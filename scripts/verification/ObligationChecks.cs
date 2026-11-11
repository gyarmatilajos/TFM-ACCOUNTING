using System.IO;
using Microsoft.EntityFrameworkCore;
using TFM_ACCOUNTING.Business.Attachments;
using TFM_ACCOUNTING.Business.Common;
using TFM_ACCOUNTING.Business.Obligations;
using TFM_ACCOUNTING.Data;
using TFM_ACCOUNTING.Data.Entities;

static class ObligationChecks
{
    public static async Task RunAsync(IDbContextFactory<AccountingDbContext> factory)
    {
        var service = new ObligationService(factory);
        var options = Require(await service.OptionsAsync());
        var companies = options.Companies.Where(x => x.IsActive).Take(2).ToArray();
        var partner = options.Partners.First(x => x.IsActive);
        var invoiceType = options.Types.First(x => x.HasInvoiceDetails);
        var periodType = options.Types.First(x => x.RequiresPeriod && !x.HasInvoiceDetails);
        var normal = options.InvoiceTypes.First(x => !x.RequiresOriginal);
        var correction = options.InvoiceTypes.First(x => x.RequiresOriginal);
        var date = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
        var performanceDate = date.AddMonths(-1);
        var draft = new ObligationInput(companies[0].Id, partner.Id, invoiceType.Id, null, "Teszt számla", null, null,
            date, 12700, null, null, null, new("CHECK-INV-1", normal.Id, date, performanceDate, 10000, 2700));
        Check((await service.SaveAsync(null, draft with { Amount = 1 })).ErrorCode == ErrorCode.Validation, "Invalid gross accepted.");
        Check((await service.SaveAsync(null, draft with { TypeId = periodType.Id, Invoice = null })).ErrorCode == ErrorCode.Validation, "Missing period accepted.");
        var id = Require(await service.SaveAsync(null, draft));
        Check((await service.SaveAsync(null, draft)).ErrorCode == ErrorCode.Duplicate, "Duplicate invoice accepted.");
        var original = Require(await service.GetAsync(id));
        Check(original.Input.Year == performanceDate.Year && original.Input.Month == performanceDate.Month, "Invoice period did not follow performance.");
        var correctedPerformance = performanceDate.AddMonths(-1);
        var movedId = Require(await service.SaveAsync(id, original.Input with
        {
            CompanyId = companies[1].Id,
            Invoice = original.Input.Invoice! with { PerformanceDate = correctedPerformance }
        }));
        Check(movedId == id, "Party change replaced obligation identity.");
        Check((await service.SaveAsync(id, original.Input)).ErrorCode == ErrorCode.ConcurrencyConflict, "Stale edit accepted.");
        var moved = Require(await service.GetAsync(id));
        Check(moved.Input.Year == correctedPerformance.Year && moved.Input.Month == correctedPerformance.Month, "Invoice period was not updated with performance.");
        Check(Require(await service.ListAsync(new(Search: "CHECK-INV-1", DateField: "period", From: correctedPerformance, To: correctedPerformance)))
            .Page.TotalCount == 1, "Invoice period filter does not use performance.");
        Check(moved.Input.CompanyId == companies[1].Id && moved.Input.Invoice?.Number == "CHECK-INV-1", "Party change did not preserve invoice.");

        var credit = draft with
        {
            CompanyId = companies[1].Id, Amount = -1270, OriginalId = id, Description = " ",
            Invoice = new("CHECK-INV-2", correction.Id, date, date, -1000, -2700 / 10m)
        };
        Check((await service.SaveAsync(null, credit with { OriginalId = null })).ErrorCode == ErrorCode.Validation, "Correction without original accepted.");
        var creditId = Require(await service.SaveAsync(null, credit));
        Check(Require(await service.GetAsync(creditId)).Input.Description is null, "Blank description did not save as null.");
        Check((await service.SaveAsync(id, moved.Input with { OriginalId = creditId })).ErrorCode == ErrorCode.Validation, "Invoice cycle accepted.");

        await using (var db = await factory.CreateDbContextAsync())
        {
            db.PaymentEntries.Add(new PaymentEntry
            {
                ObligationId = id, SettlementTypeId = 1, PaymentMethodId = 1, SettlementDate = date, Amount = 5000
            });
            await db.SaveChangesAsync();
        }
        Check((await service.SaveAsync(id, moved.Input with
        {
            Amount = 1000, Invoice = moved.Input.Invoice! with { Net = 1000, Vat = 0 }
        })).ErrorCode == ErrorCode.Validation, "Over-settled edit accepted.");
        var page = Require(await service.ListAsync(new(Search: "CHECK-INV-", NoCategory: true, DateField: "issue", From: date, To: date)));
        Check(page.Page.TotalCount == 2 && page.Payable == 7700 && page.Credit == 1270, "Filtered balances incorrect.");
        var today = DateOnly.FromDateTime(DateTime.Today);
        var expectedMonthlyPaid = date.Year == today.Year && date.Month == today.Month ? 5000m : 0m;
        Check(page.Overdue == 7700 && page.DueSoon == 0 && page.PaidThisMonth == expectedMonthlyPaid,
            "Dashboard amounts do not follow the filtered rows and settlement dates.");
        Check(Require(await service.ExportRowsAsync(new(Search: "CHECK-INV-", Page: 2))).Count == 2, "Export incorrectly uses the current page.");
        Check(Require(await service.ListAsync(new(Search: "CHECK-INV-", Status: "partial"))).Page.TotalCount == 1, "Payment filter incorrect.");
        Check(Require(await service.ListAsync(new(Search: "CHECK-INV-", Status: "partial", DueState: "overdue"))).Page.TotalCount == 1, "Due-date and payment filters do not combine.");
        Require(await service.SetArchivedAsync(id, true, moved.Input.Version!));
        Check(Require(await service.ListAsync(new(Search: "CHECK-INV-"))).Page.TotalCount == 1, "Default list contains archived invoice.");
        Check(Require(await service.ListAsync(new(Search: "CHECK-INV-", Archived: null))).Payable == 7700, "Archived balance missing.");
        var archived = Require(await service.GetAsync(id));
        Check(archived.Settlements.Single().Amount == 5000, "Details omit settlement.");
        Require(await service.SetArchivedAsync(id, false, archived.Input.Version!));
        var salary = draft with { TypeId = periodType.Id, Description = "Teszt havi tétel", Year = date.Year, Month = date.Month, Invoice = null };
        var salaryId = Require(await service.SaveAsync(null, salary));
        Check(Require(await service.ListAsync(new(Search: salary.Description, DateField: "period", From: date, To: date))).Page.TotalCount == 1, "Period filter incorrect.");
        Check(Require(await service.ListAsync(new(Search: salary.Description, QuickType: "invoices"))).Page.TotalCount == 0,
            "The invoice tab includes a non-invoice obligation.");
        await CheckSettlementsAsync(service, id, creditId, salaryId, date);
        await CheckAttachmentsAsync(factory, id);
        Console.WriteLine("PASS: obligations, invoice party changes, original links, periods, duplicate checks, concurrency, filtered balances and archive.");
    }

    private static async Task CheckAttachmentsAsync(IDbContextFactory<AccountingDbContext> factory, int obligation)
    {
        var workspace = Path.GetFullPath("obj/ModelVerification");
        var folder = Path.Combine(workspace, "AttachmentCheck-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var root = Directory.CreateDirectory(Path.Combine(folder, "Sync")).FullName;
            var configFile = Path.Combine(folder, "settings.txt");
            var settings = new AttachmentFolderSettings(configFile);
            Require(settings.Save(root));
            Check(Require(new AttachmentFolderSettings(configFile).Read()) == root, "Attachment settings did not persist.");
            Check(!settings.Save(Path.Combine(folder, "Missing")).IsSuccess && Require(settings.Read()) == root,
                "A missing folder replaced the valid attachment setting.");

            var service = new AttachmentService(factory, settings);
            const string filename = "Műszaki számla ő.pdf";
            var source = Path.Combine(folder, filename);
            await File.WriteAllTextAsync(source, "%PDF-1.4\nTest attachment");
            var id = Require(await service.UploadAsync(obligation, source));
            var duplicateName = Require(await service.UploadAsync(obligation, source));
            var original = Require(await service.OpenPathAsync(obligation, id));
            var other = Require(await service.OpenPathAsync(obligation, duplicateName));
            Check(original != other && File.Exists(source),
                "Uploading a duplicate filename overwrote a document or moved the original.");
            Check(Path.GetDirectoryName(original) == root && Path.GetFileName(original).StartsWith("Műszaki számla ő-") &&
                Path.GetExtension(original) == ".pdf", "The attachment did not retain its name in the root folder.");
            const string legacyRelative = "5/3/legacy.pdf";
            var legacy = Path.GetFullPath(Path.Combine(root, legacyRelative));
            Directory.CreateDirectory(Path.GetDirectoryName(legacy)!);
            File.Move(other, legacy);
            await using (var db = await factory.CreateDbContextAsync())
            {
                var relative = await db.Attachments.Where(x => x.Id == id).Select(x => x.RelativePath).SingleAsync();
                Check(!Path.IsPathRooted(relative) && Path.GetFullPath(Path.Combine(root, relative)) == original,
                    "The database stored a machine-specific attachment path.");
                var legacyEntry = await db.Attachments.SingleAsync(x => x.Id == duplicateName);
                legacyEntry.RelativePath = legacyRelative;
                await db.SaveChangesAsync();
            }
            Check(Require(await service.OpenPathAsync(obligation, duplicateName)) == legacy, "Legacy attachment path is no longer readable.");
            var copy = Path.Combine(folder, "Másolat.pdf");
            Require(await service.SaveCopyAsync(obligation, id, copy));
            Check(await File.ReadAllTextAsync(source) == await File.ReadAllTextAsync(copy), "Saved copy differs.");
            var row = Require(await service.ListAsync(obligation)).Single(x => x.Id == id);
            Require(await service.UpdateNotesAsync(obligation, id, row.Version, " Teszt megjegyzés "));
            Check((await service.RemoveAsync(obligation, id, row.Version)).ErrorCode == ErrorCode.ConcurrencyConflict &&
                File.Exists(original), "A stale removal moved the file.");

            var current = Require(await service.ListAsync(obligation)).Single(x => x.Id == id);
            Check(current.Notes == "Teszt megjegyzés", "Notes did not save.");
            var notSynced = original + ".offline";
            File.Move(original, notSynced);
            Check((await service.OpenPathAsync(obligation, id)).ErrorCode == ErrorCode.StorageUnavailable &&
                (await service.RemoveAsync(obligation, id, current.Version)).ErrorCode == ErrorCode.StorageUnavailable &&
                Require(await service.ListAsync(obligation)).Any(x => x.Id == id), "A missing sync file lost its database record.");
            File.Move(notSynced, original);
            Require(await service.RemoveAsync(obligation, id, current.Version));
            var trash = Path.Combine(root, AttachmentService.DeletedFolder);
            Check(!File.Exists(original) && !Require(await service.ListAsync(obligation)).Any(x => x.Id == id) &&
                Directory.EnumerateFiles(trash, filename, SearchOption.AllDirectories).Count() == 1,
                "Removing the attachment did not preserve the file in the deleted documents folder.");
            Console.WriteLine("PASS: attachment folder, duplicate filenames, relative paths, saved copy, notes, missing sync files and recoverable removal.");
        }
        finally
        {
            if (Path.GetDirectoryName(Path.GetFullPath(folder)) == workspace &&
                Path.GetFileName(folder).StartsWith("AttachmentCheck-", StringComparison.Ordinal))
                Directory.Delete(folder, recursive: true);
        }
    }

    private static async Task CheckSettlementsAsync(ObligationService service, int invoice, int credit, int salary, DateOnly date)
    {
        var screen = Require(await service.GetSettlementsAsync(invoice));
        var cashType = screen.Types.First(x => x.IsActive && !x.IsOffset).Id;
        var offsetType = screen.Types.First(x => x.IsActive && x.IsOffset).Id;
        var method = screen.Methods.First(x => x.IsActive).Id;
        Check(screen.Candidates.Any(x => x.Id == credit && x.Balance == -1270), "Available credit missing.");
        var offset = new SettlementInput(date, 500, offsetType, null, credit, null, null);
        Check((await service.SaveSettlementAsync(invoice, offset with { Amount = 1271 })).ErrorCode == ErrorCode.Validation,
            "Offset exceeding the available credit was accepted.");
        Require(await service.SaveSettlementAsync(invoice, offset));
        var positive = Require(await service.GetSettlementsAsync(invoice));
        var negative = Require(await service.GetSettlementsAsync(credit));
        var pair = positive.Rows.Single(x => x.IsOffset);
        var cashReport = Require(await service.CashMovementsAsync(date, date));
        Check(cashReport.Rows.Count == 1 && cashReport.Paid == 5000 && cashReport.Refunded == 0,
            "Credit offset was included in the cash report.");
        Check(positive.Balance == 7200 && negative.Balance == -770 && negative.Rows.Single().Amount == -500 &&
            pair.Version.CounterpartId == negative.Rows.Single().Version.Id, "Offset pair or balances are incorrect.");
        Require(await service.SaveSettlementAsync(invoice, offset with { Amount = 800, OtherObligationId = null, Existing = pair.Version }));
        Check(Require(await service.GetSettlementsAsync(credit)).Balance == -470, "Editing the offset did not update both entries.");
        Check((await service.DeleteSettlementAsync(invoice, pair.Version)).ErrorCode == ErrorCode.ConcurrencyConflict,
            "A stale offset could be deleted.");
        pair = Require(await service.GetSettlementsAsync(invoice)).Rows.Single(x => x.IsOffset);
        Require(await service.DeleteSettlementAsync(invoice, pair.Version));
        Check(Require(await service.GetSettlementsAsync(credit)).Rows.Count == 0 &&
            Require(await service.GetSettlementsAsync(invoice)).Balance == 7700, "Reversing the offset did not restore both balances.");

        var payment = new SettlementInput(date, 0, cashType, method, null, "Teszt átutalás", null, FullBalance: true);
        Require(await service.SaveSettlementAsync(invoice, payment));
        Require(await service.SaveSettlementAsync(credit, payment));
        positive = Require(await service.GetSettlementsAsync(invoice));
        negative = Require(await service.GetSettlementsAsync(credit));
        Check(positive.Balance == 0 && negative.Balance == 0 && negative.Rows.Single().Amount == -1270, "Full settlement or refund has the wrong sign.");
        var last = positive.Rows.First();
        Require(await service.SaveSettlementAsync(invoice, payment with { Amount = 7000, FullBalance = false, Existing = last.Version }));
        Check(Require(await service.GetSettlementsAsync(invoice)).Balance == 700, "Editing a payment did not exclude the original amount.");

        var race = payment with { Amount = 10000, FullBalance = false };
        var concurrent = await Task.WhenAll(service.SaveSettlementAsync(salary, race), service.SaveSettlementAsync(salary, race));
        Check(concurrent.Count(x => x.IsSuccess) == 1 && Require(await service.GetSettlementsAsync(salary)).Balance == 2700,
            "Concurrent payments exceeded the outstanding amount.");
        Console.WriteLine("PASS: cash payments, refunds, linked credit offsets, paired edits and reversals, and concurrent balance protection.");
    }

    private static T Require<T>(Response<T> result)
    {
        if (!result.IsSuccess)
            throw new InvalidOperationException(result.Message + " " + string.Join(" ", result.ValidationErrors.Select(x => x.Message)));
        return result.Data!;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
