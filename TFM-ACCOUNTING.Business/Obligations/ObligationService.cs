using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TFM_ACCOUNTING.Business.Common;
using TFM_ACCOUNTING.Data;
using TFM_ACCOUNTING.Data.Entities;

namespace TFM_ACCOUNTING.Business.Obligations;

public sealed partial class ObligationService(IDbContextFactory<AccountingDbContext> factory)
{
    public Task<Response<ObligationOptions>> OptionsAsync() => Run(async db =>
    {
        var companies = await db.OwnCompanies.OrderBy(x => x.Name).Select(x => new ObligationOption(x.Id, x.ShortName ?? x.Name, x.IsActive, false, false, false)).ToListAsync();
        var partners = await db.Partners.OrderBy(x => x.Name).Select(x => new ObligationOption(x.Id, x.ShortName ?? x.Name, x.IsActive, false, false, false)).ToListAsync();
        var types = await db.ObligationTypes.OrderBy(x => x.Name).Select(x => new ObligationOption(x.Id, x.Name, x.IsActive, x.RequiresPeriod, x.HasInvoiceDetails, false)).ToListAsync();
        var categories = await db.CostCategories.OrderBy(x => x.Name).Select(x => new ObligationOption(x.Id, x.Name, x.IsActive, false, false, false)).ToListAsync();
        var methods = await db.PaymentMethods.OrderBy(x => x.Name).Select(x => new ObligationOption(x.Id, x.Name, x.IsActive, false, false, false)).ToListAsync();
        var invoices = await db.InvoiceTypes.OrderBy(x => x.Name).Select(x => new ObligationOption(x.Id, x.Name, x.IsActive, false, false, x.RequiresOriginalInvoice)).ToListAsync();
        return Response<ObligationOptions>.Success(new(companies, partners, types, categories, methods, invoices));
    });

    public Task<Response<IReadOnlyList<ObligationOption>>> OriginalsAsync(int company, int partner, int? current) => Run<IReadOnlyList<ObligationOption>>(async db =>
    {
        var items = await db.InvoiceDetails.Where(x => x.OwnCompanyId == company && x.PartnerId == partner && x.ObligationId != current)
            .OrderByDescending(x => x.IssueDate).Select(x => new ObligationOption(x.ObligationId, x.InvoiceNumber, true, false, false, false)).ToListAsync();
        return Response<IReadOnlyList<ObligationOption>>.Success(items);
    });

    public Task<Response<ObligationDetails>> GetAsync(int id) => Run(async db =>
    {
        var row = await db.Obligations.AsNoTracking().Include(x => x.InvoiceDetails).SingleOrDefaultAsync(x => x.Id == id);
        if (row is null) return Missing<ObligationDetails>();
        var settlements = await db.PaymentEntries.Where(x => x.ObligationId == id).OrderBy(x => x.SettlementDate).ThenBy(x => x.Id)
            .Select(x => new SettlementSummary(x.SettlementDate, x.Amount, x.SettlementType.Name, x.PaymentMethod == null ? null : x.PaymentMethod.Name)).ToListAsync();
        var attachments = await db.Attachments.Where(x => x.ObligationId == id).OrderBy(x => x.Id)
            .Select(x => new AttachmentSummary(x.OriginalFileName, x.FileSizeBytes, x.Notes)).ToListAsync();
        var invoice = row.InvoiceDetails;
        var input = new ObligationInput(row.OwnCompanyId, row.PartnerId, row.ObligationTypeId, row.CategoryId,
            row.Description, row.PeriodYear, row.PeriodMonth, row.DueDate, row.Amount, row.PaymentMethodId,
            row.OriginalObligationId, row.Notes, invoice is null ? null : new(invoice.InvoiceNumber, invoice.InvoiceTypeId,
                invoice.IssueDate, invoice.PerformanceDate, invoice.NetAmount, invoice.VatAmount, invoice.RowVersion), row.RowVersion);
        return Response<ObligationDetails>.Success(new(row.Id, input, row.IsArchived, settlements, attachments));
    });

    public Task<Response<ObligationPage>> ListAsync(ObligationQuery filter) => Run(async db =>
    {
        if (!ValidFilter(filter))
            return Response<ObligationPage>.Invalid([new("Filter", "Ellenőrizd a tartományokat és az oldalszámot.")]);
        var amounts = FilterRows(db, filter);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var nextWeek = today.AddDays(7);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var totals = await amounts.GroupBy(_ => 1).Select(group => new
        {
            Count = group.Count(),
            Payable = group.Sum(x => x.Row.Amount > x.Settled ? x.Row.Amount - x.Settled : 0m),
            Credit = group.Sum(x => x.Row.Amount < x.Settled ? x.Settled - x.Row.Amount : 0m),
            Overdue = group.Sum(x => x.Row.Amount > x.Settled && x.Row.DueDate < today ? x.Row.Amount - x.Settled : 0m),
            DueSoon = group.Sum(x => x.Row.Amount > x.Settled && x.Row.DueDate >= today && x.Row.DueDate <= nextWeek
                ? x.Row.Amount - x.Settled : 0m)
        }).SingleOrDefaultAsync();
        var filteredIds = amounts.Select(x => x.Row.Id);
        var paid = await db.PaymentEntries.Where(x => x.Amount > 0 && x.SettlementGroupId == null &&
            !x.SettlementType.IsCreditOffset && x.SettlementDate >= monthStart && x.SettlementDate <= today &&
            filteredIds.Contains(x.ObligationId)).SumAsync(x => (decimal?)x.Amount) ?? 0m;
        var items = await Project(amounts).Skip((filter.Page - 1) * 25).Take(25).ToListAsync();
        return Response<ObligationPage>.Success(new(new(items, totals?.Count ?? 0, filter.Page, 25),
            totals?.Payable ?? 0m, totals?.Credit ?? 0m, totals?.Overdue ?? 0m, totals?.DueSoon ?? 0m, paid));
    });

    public Task<Response<IReadOnlyList<ObligationRow>>> ExportRowsAsync(ObligationQuery filter) => Run<IReadOnlyList<ObligationRow>>(async db =>
    {
        if (!ValidFilter(filter))
            return Response<IReadOnlyList<ObligationRow>>.Invalid([new("Filter", "Ellenőrizd a szűrőket.")]);
        var items = await Project(FilterRows(db, filter)).ToListAsync();
        return Response<IReadOnlyList<ObligationRow>>.Success(items);
    });

    private static bool ValidFilter(ObligationQuery filter) => filter.Page >= 1 && filter.Page <= int.MaxValue / 25 &&
        !(filter.From > filter.To) && !(filter.Min > filter.Max);

    private sealed class BalanceRow
    {
        public required Obligation Row { get; init; }
        public decimal Settled { get; init; }
    }

    private static IQueryable<ObligationRow> Project(IQueryable<BalanceRow> amounts) =>
        amounts.OrderBy(x => x.Row.DueDate).ThenBy(x => x.Row.Id)
            .Select(x => new ObligationRow(x.Row.Id, x.Row.OwnCompany.ShortName ?? x.Row.OwnCompany.Name,
                x.Row.Partner.ShortName ?? x.Row.Partner.Name, x.Row.ObligationType.Name, x.Row.Description,
                x.Row.InvoiceDetails == null ? null : x.Row.InvoiceDetails.InvoiceNumber, x.Row.DueDate,
                x.Row.Amount, x.Settled, x.Row.IsArchived, x.Row.Attachments.Any()));

    private static IQueryable<BalanceRow> FilterRows(AccountingDbContext db, ObligationQuery filter)
    {
        var rows = db.Obligations.AsNoTracking().AsQueryable();
        if (filter.CompanyId is int company) rows = rows.Where(x => x.OwnCompanyId == company);
        if (filter.PartnerId is int partner) rows = rows.Where(x => x.PartnerId == partner);
        if (filter.TypeId is int type) rows = rows.Where(x => x.ObligationTypeId == type);
        if (filter.QuickType == "invoices") rows = rows.Where(x => x.ObligationType.HasInvoiceDetails);
        else if (filter.QuickType is string code) rows = rows.Where(x => x.ObligationType.Code == code);
        if (filter.CategoryId is int category) rows = rows.Where(x => x.CategoryId == category);
        if (filter.NoCategory) rows = rows.Where(x => x.CategoryId == null);
        if (filter.Archived is bool archived) rows = rows.Where(x => x.IsArchived == archived);
        if (filter.HasAttachment is bool attached) rows = rows.Where(x => x.Attachments.Any() == attached);
        if (filter.Min is decimal min) rows = rows.Where(x => x.Amount >= min);
        if (filter.Max is decimal max) rows = rows.Where(x => x.Amount <= max);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            rows = rows.Where(x => (x.Description != null && x.Description.Contains(term)) ||
                (x.InvoiceDetails != null && x.InvoiceDetails.InvoiceNumber.Contains(term)));
        }
        if (filter.From is DateOnly from) rows = filter.DateField switch
        {
            "issue" => rows.Where(x => x.InvoiceDetails != null && x.InvoiceDetails.IssueDate >= from),
            "performance" => rows.Where(x => x.InvoiceDetails != null && x.InvoiceDetails.PerformanceDate >= from),
            "period" => rows.Where(x => x.PeriodYear * 100 + x.PeriodMonth >= from.Year * 100 + from.Month),
            _ => rows.Where(x => x.DueDate >= from)
        };
        if (filter.To is DateOnly to) rows = filter.DateField switch
        {
            "issue" => rows.Where(x => x.InvoiceDetails != null && x.InvoiceDetails.IssueDate <= to),
            "performance" => rows.Where(x => x.InvoiceDetails != null && x.InvoiceDetails.PerformanceDate <= to),
            "period" => rows.Where(x => x.PeriodYear * 100 + x.PeriodMonth <= to.Year * 100 + to.Month),
            _ => rows.Where(x => x.DueDate <= to)
        };

        var amounts = rows.Select(x => new BalanceRow { Row = x, Settled = x.PaymentEntries.Sum(p => (decimal?)p.Amount) ?? 0m });
        var today = DateOnly.FromDateTime(DateTime.Today);
        var nextWeek = today.AddDays(7);
        amounts = filter.Status switch
        {
            "open" => amounts.Where(x => x.Settled == 0),
            "partial" => amounts.Where(x => x.Settled != 0 && x.Settled != x.Row.Amount),
            "settled" => amounts.Where(x => x.Settled == x.Row.Amount),
            _ => amounts
        };
        return filter.DueState switch
        {
            "overdue" => amounts.Where(x => x.Row.Amount > x.Settled && x.Row.DueDate < today),
            "week" => amounts.Where(x => x.Row.Amount > x.Settled && x.Row.DueDate >= today && x.Row.DueDate <= nextWeek),
            _ => amounts
        };
    }

    public Task<Response<int>> SaveAsync(int? id, ObligationInput input) => Write(async db =>
    {
        var row = id is int key ? await db.Obligations.Include(x => x.InvoiceDetails).SingleOrDefaultAsync(x => x.Id == key) : null;
        if (id.HasValue && row is null) return Missing<int>();
        var oldInvoice = row?.InvoiceDetails;
        if (row is not null && (!SameVersion(row.RowVersion, input.Version) ||
            (oldInvoice is not null && input.Invoice is not null && !SameVersion(oldInvoice.RowVersion, input.Invoice.Version))))
            return Conflict<int>();

        // Invoice periods always follow performance, including when the invoice is edited.
        if (input.Invoice is { } invoicePeriod)
            input = input with { Year = invoicePeriod.PerformanceDate.Year, Month = invoicePeriod.PerformanceDate.Month };

        var error = await ValidateAsync(db, row, input);
        if (error is not null) return Response<int>.Invalid([error]);
        if (input.Invoice is { } invoice && await db.InvoiceDetails.AnyAsync(x =>
            x.OwnCompanyId == input.CompanyId && x.PartnerId == input.PartnerId &&
            x.InvoiceNumber == invoice.Number.Trim() && x.ObligationId != id))
            return Duplicate<int>();

        // The party columns are an EF alternate key. Change them only after removing the dependent invoice row.
        if (row is not null && (row.OwnCompanyId != input.CompanyId || row.PartnerId != input.PartnerId))
        {
            if (oldInvoice is not null)
            {
                db.InvoiceDetails.Remove(oldInvoice);
                await db.SaveChangesAsync();
            }
            await db.Obligations.Where(x => x.Id == id).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.OwnCompanyId, input.CompanyId).SetProperty(x => x.PartnerId, input.PartnerId));
            db.ChangeTracker.Clear();
            row = await db.Obligations.SingleAsync(x => x.Id == id);
            oldInvoice = null;
        }
        if (row is null)
        {
            row = new Obligation { OwnCompanyId = input.CompanyId, PartnerId = input.PartnerId };
            db.Obligations.Add(row);
        }
        else db.Entry(row).Property(x => x.UpdatedAtUtc).IsModified = true;

        row.ObligationTypeId = input.TypeId;
        row.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        row.CategoryId = input.CategoryId;
        row.PeriodYear = input.Year;
        row.PeriodMonth = input.Month;
        row.DueDate = input.DueDate;
        row.Amount = input.Amount;
        row.PaymentMethodId = input.PaymentMethodId;
        row.OriginalObligationId = input.OriginalId;
        row.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();

        if (input.Invoice is { } details)
        {
            var target = oldInvoice ?? new InvoiceDetails { Obligation = row, InvoiceNumber = details.Number.Trim() };
            target.InvoiceNumber = details.Number.Trim();
            target.InvoiceTypeId = details.TypeId;
            target.IssueDate = details.IssueDate;
            target.PerformanceDate = details.PerformanceDate;
            target.NetAmount = details.Net;
            target.VatAmount = details.Vat;
            if (oldInvoice is null) db.InvoiceDetails.Add(target);
        }
        else if (oldInvoice is not null) db.InvoiceDetails.Remove(oldInvoice);

        await db.SaveChangesAsync();
        return Response<int>.Success(row.Id, "A tételt mentettük.");
    });

    public Task<Response<int>> SetArchivedAsync(int id, bool archived, byte[] version) => Write(async db =>
    {
        var row = await db.Obligations.SingleOrDefaultAsync(x => x.Id == id);
        if (row is null) return Missing<int>();
        if (!SameVersion(row.RowVersion, version)) return Conflict<int>();
        row.IsArchived = archived;
        await db.SaveChangesAsync();
        return Response<int>.Success(id, archived ? "A tételt archiváltuk." : "A tételt visszaállítottuk.");
    });

    private static async Task<ValidationError?> ValidateAsync(AccountingDbContext db, Obligation? row, ObligationInput input)
    {
        if (input.Description?.Trim().Length > 200)
            return new("Description", "A leírás legfeljebb 200 karakteres lehet.");
        if (input.Notes?.Trim().Length > 2000)
            return new("Notes", "A megjegyzés legfeljebb 2000 karakteres lehet.");
        if (input.DueDate == default || input.Amount == 0 || !ValidAmount(input.Amount))
            return new("Amount", "Adj meg határidőt és nem nulla összeget, legfeljebb két tizedesjeggyel.");

        var previousCompany = row?.OwnCompanyId;
        var previousPartner = row?.PartnerId;
        var previousCategory = row?.CategoryId;
        var previousMethod = row?.PaymentMethodId;
        if (!await db.OwnCompanies.AnyAsync(x => x.Id == input.CompanyId && (x.IsActive || x.Id == previousCompany)))
            return new("CompanyId", "Válassz aktív saját céget.");
        if (!await db.Partners.AnyAsync(x => x.Id == input.PartnerId && (x.IsActive || x.Id == previousPartner)))
            return new("PartnerId", "Válassz aktív partnert.");
        var type = await db.ObligationTypes.FindAsync(input.TypeId);
        if (type is null || (!type.IsActive && row?.ObligationTypeId != type.Id))
            return new("TypeId", "Válassz aktív kötelezettségtípust.");
        if (input.CategoryId is int category && !await db.CostCategories.AnyAsync(x => x.Id == category && (x.IsActive || x.Id == previousCategory)))
            return new("CategoryId", "Válassz aktív költségkategóriát.");
        if (input.PaymentMethodId is int method && !await db.PaymentMethods.AnyAsync(x => x.Id == method && (x.IsActive || x.Id == previousMethod)))
            return new("PaymentMethodId", "Válassz aktív fizetési módot.");
        if (input.Year.HasValue != input.Month.HasValue || input.Year is < 1 or > 9999 ||
            input.Month is < 1 or > 12 || (type.RequiresPeriod && !input.Year.HasValue))
            return new("Period", "Add meg együtt az évet és az 1–12 közötti hónapot.");
        if (type.HasInvoiceDetails != (input.Invoice is not null))
            return new("Invoice", "A választott típushoz illő számlaadatokat add meg.");

        if (input.Invoice is { } invoice)
        {
            var invoiceType = await db.InvoiceTypes.FindAsync(invoice.TypeId);
            if (invoiceType is null || (!invoiceType.IsActive && row?.InvoiceDetails?.InvoiceTypeId != invoice.TypeId))
                return new("InvoiceType", "Válassz aktív számlatípust.");
            if (string.IsNullOrWhiteSpace(invoice.Number) || invoice.Number.Trim().Length > 100 ||
                invoice.IssueDate == default || invoice.PerformanceDate == default)
                return new("Invoice", "A számlaszám, a kiállítás és a teljesítés dátuma kötelező.");
            if (!ValidAmount(invoice.Net) || !ValidAmount(invoice.Vat) || invoice.Net + invoice.Vat != input.Amount)
                return new("Invoice", "A nettó és az áfa összege egyezzen a fizetendő összeggel.");
            if (invoiceType.RequiresOriginalInvoice && !input.OriginalId.HasValue)
                return new("OriginalId", "Válaszd ki az eredeti számlát.");
        }
        else if (input.OriginalId.HasValue) return new("OriginalId", "Eredeti számlát számlához lehet kapcsolni.");

        var seen = new HashSet<int>();
        if (row is not null) seen.Add(row.Id);
        for (var original = input.OriginalId; original is int originalId;)
        {
            if (!seen.Add(originalId)) return new("OriginalId", "A számlahivatkozás nem lehet körkörös.");
            var target = await db.Obligations.Where(x => x.Id == originalId && x.OwnCompanyId == input.CompanyId &&
                x.PartnerId == input.PartnerId && x.InvoiceDetails != null)
                .Select(x => new { x.OriginalObligationId }).SingleOrDefaultAsync();
            if (target is null) return new("OriginalId", "Az eredeti számla ugyanahhoz a céghez és partnerhez tartozzon.");
            original = target.OriginalObligationId;
        }
        if (row is null) return null;
        var partiesChanged = row.OwnCompanyId != input.CompanyId || row.PartnerId != input.PartnerId;
        if ((partiesChanged || input.Invoice is null) && await db.Obligations.AnyAsync(x => x.OriginalObligationId == row.Id))
            return new("OriginalId", "Másik számla hivatkozik erre a számlára. A cég, a partner és a számlajelleg nem változtatható.");
        var payments = db.PaymentEntries.Where(x => x.ObligationId == row.Id);
        if (await payments.AnyAsync())
        {
            if (partiesChanged || row.ObligationTypeId != input.TypeId || Math.Sign(row.Amount) != Math.Sign(input.Amount))
                return new("Amount", "Rendezéssel rendelkező tételnél a cég, partner, típus és előjel nem változtatható.");
            if (Math.Abs(await payments.SumAsync(x => x.Amount)) > Math.Abs(input.Amount))
                return new("Amount", "A fizetendő összeg nem lehet kisebb a már rendezett összegnél.");
        }
        return null;
    }

    private static bool ValidAmount(decimal value) => Math.Abs(value) <= 9999999999999999.99m && decimal.Round(value, 2) == value;
    private static bool SameVersion(byte[] current, byte[]? original) => original is not null && current.AsSpan().SequenceEqual(original);
    private static Response<T> Missing<T>() => Response<T>.Failure(ErrorCode.NotFound, "A tétel már nem található.");
    private static Response<T> Conflict<T>() => Response<T>.Failure(ErrorCode.ConcurrencyConflict, "A tétel időközben módosult. Frissítsd a listát.");
    private static Response<T> Duplicate<T>() => Response<T>.Failure(ErrorCode.Duplicate, "Ez a számlaszám már szerepel ennél a cégnél és partnernél.");

    private Task<Response<T>> Write<T>(Func<AccountingDbContext, Task<Response<T>>> action) => Run(async db =>
    {
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var attempt = await factory.CreateDbContextAsync();
            await using var transaction = await attempt.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var result = await action(attempt);
            if (result.IsSuccess)
            {
                try { await transaction.CommitAsync(); }
                catch (Exception e) when (e is SqlException or TimeoutException)
                {
                    // A lost commit response does not establish whether the write succeeded.
                    Trace.TraceError("Obligation commit outcome unknown ({0}).", e.GetType().Name);
                    return Response<T>.Failure(ErrorCode.DatabaseUnavailable,
                        "A mentés eredménye bizonytalan. Frissítsd az adatokat, mielőtt megismétled.");
                }
            }
            return result;
        });
    });

    private async Task<Response<T>> Run<T>(Func<AccountingDbContext, Task<Response<T>>> action)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync();
            return await action(db);
        }
        catch (DbUpdateConcurrencyException) { return Conflict<T>(); }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 }) { return Duplicate<T>(); }
        catch (SqlException e) when (e.Number == 1205) { return Conflict<T>(); }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 1205 }) { return Conflict<T>(); }
        catch (Exception e)
        {
            Trace.TraceError("Obligation operation failed ({0}).", e.GetType().Name);
            var code = e is SqlException or DbUpdateException or TimeoutException ? ErrorCode.DatabaseUnavailable : ErrorCode.Unexpected;
            return Response<T>.Failure(code, "A tételművelet nem sikerült. Ellenőrizd az adatbázis-kapcsolatot, majd próbáld újra.");
        }
    }
}


