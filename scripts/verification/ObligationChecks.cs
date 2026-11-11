using Microsoft.EntityFrameworkCore;
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
        var draft = new ObligationInput(companies[0].Id, partner.Id, invoiceType.Id, null, "Teszt számla", null, null,
            date, 12700, null, null, null, new("CHECK-INV-1", normal.Id, date, date, 10000, 2700));
        Check((await service.SaveAsync(null, draft with { Amount = 1 })).ErrorCode == ErrorCode.Validation, "Invalid gross accepted.");
        Check((await service.SaveAsync(null, draft with { TypeId = periodType.Id, Invoice = null })).ErrorCode == ErrorCode.Validation, "Missing period accepted.");
        var id = Require(await service.SaveAsync(null, draft));
        Check((await service.SaveAsync(null, draft)).ErrorCode == ErrorCode.Duplicate, "Duplicate invoice accepted.");
        var original = Require(await service.GetAsync(id));
        var movedId = Require(await service.SaveAsync(id, original.Input with { CompanyId = companies[1].Id }));
        Check(movedId == id, "Party change replaced obligation identity.");
        Check((await service.SaveAsync(id, original.Input)).ErrorCode == ErrorCode.ConcurrencyConflict, "Stale edit accepted.");
        var moved = Require(await service.GetAsync(id));
        Check(moved.Input.CompanyId == companies[1].Id && moved.Input.Invoice?.Number == "CHECK-INV-1", "Party change did not preserve invoice.");

        var credit = draft with
        {
            CompanyId = companies[1].Id, Amount = -1270, OriginalId = id,
            Invoice = new("CHECK-INV-2", correction.Id, date, date, -1000, -2700 / 10m)
        };
        Check((await service.SaveAsync(null, credit with { OriginalId = null })).ErrorCode == ErrorCode.Validation, "Correction without original accepted.");
        var creditId = Require(await service.SaveAsync(null, credit));
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
        Require(await service.SaveAsync(null, salary));
        Check(Require(await service.ListAsync(new(Search: salary.Description, DateField: "period", From: date, To: date))).Page.TotalCount == 1, "Period filter incorrect.");
        Console.WriteLine("PASS: obligations, invoice party changes, original links, periods, duplicate checks, concurrency, filtered balances and archive.");
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
