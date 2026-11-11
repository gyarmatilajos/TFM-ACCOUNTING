using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TFM_ACCOUNTING.Business.Common;
using TFM_ACCOUNTING.Data;
using TFM_ACCOUNTING.Data.Entities;

namespace TFM_ACCOUNTING.Business.OwnCompanies;

public sealed class OwnCompanyService(IDbContextFactory<AccountingDbContext> contextFactory) : IOwnCompanyService
{
    public Task<Response<PageResult<OwnCompanyListItem>>> ListAsync(OwnCompanyQuery query,
        CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue || query.Search?.Trim().Length > 200 ||
            query.SortBy is not ("Name" or "ShortName" or "TaxNumber" or "IsActive"))
            return Response<PageResult<OwnCompanyListItem>>.Invalid([new("Query", "Érvénytelen keresés, rendezés vagy lapozás.")]);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = db.OwnCompanies.AsNoTracking();
        if (query.IsActive.HasValue) rows = rows.Where(x => x.IsActive == query.IsActive.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x => x.Name.Contains(search) || (x.ShortName != null && x.ShortName.Contains(search)) || x.TaxNumber.Contains(search));
        }
        var total = await rows.CountAsync(cancellationToken);
        IOrderedQueryable<OwnCompany> sorted = (query.SortBy, query.Descending) switch
        {
            ("ShortName", false) => rows.OrderBy(x => x.ShortName ?? x.Name),
            ("ShortName", true) => rows.OrderByDescending(x => x.ShortName ?? x.Name),
            ("TaxNumber", false) => rows.OrderBy(x => x.TaxNumber),
            ("TaxNumber", true) => rows.OrderByDescending(x => x.TaxNumber),
            ("IsActive", false) => rows.OrderBy(x => x.IsActive),
            ("IsActive", true) => rows.OrderByDescending(x => x.IsActive),
            (_, true) => rows.OrderByDescending(x => x.Name),
            _ => rows.OrderBy(x => x.Name)
        };
        var skip = (long)(query.Page - 1) * query.PageSize;
        if (skip > int.MaxValue)
            return Response<PageResult<OwnCompanyListItem>>.Invalid([new("Page", "Túl nagy oldalszám.")]);
        var items = await sorted.ThenBy(x => x.Id).Skip((int)skip).Take(query.PageSize)
            .Select(x => new OwnCompanyListItem(x.Id, x.Name, x.ShortName, x.TaxNumber, x.City, x.IsActive))
            .ToListAsync(cancellationToken);
        return Response<PageResult<OwnCompanyListItem>>.Success(new(items, total, query.Page, query.PageSize));
    }, cancellationToken);

    public Task<Response<OwnCompanyDetails>> GetAsync(int id, CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var company = await db.OwnCompanies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return company is null ? Missing() : Response<OwnCompanyDetails>.Success(ToDetails(company));
    }, cancellationToken);

    public Task<Response<OwnCompanyDetails>> SaveAsync(SaveOwnCompanyRequest request, CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        var errors = Validate(request);
        if (errors.Count > 0) return Response<OwnCompanyDetails>.Invalid(errors);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var company = request.Id.HasValue
            ? await db.OwnCompanies.SingleOrDefaultAsync(x => x.Id == request.Id.Value, cancellationToken) : null;
        if (request.Id.HasValue && company is null) return Missing();
        if (company is not null && !company.RowVersion.SequenceEqual(request.RowVersion!)) return Conflict();

        var taxNumber = request.GenerateInternalIdentifier
            ? (company?.TaxNumber.StartsWith("BELSO-", StringComparison.OrdinalIgnoreCase) == true
                ? company.TaxNumber : "BELSO-" + Guid.NewGuid().ToString("N")[..14].ToUpperInvariant())
            : request.TaxNumber!.Trim();
        if (await db.OwnCompanies.AnyAsync(x => x.TaxNumber == taxNumber && (!request.Id.HasValue || x.Id != request.Id.Value), cancellationToken))
            return Response<OwnCompanyDetails>.Failure(ErrorCode.Duplicate, "Ez az adószám / azonosító már szerepel egy másik saját cégnél.");

        var isNew = company is null;
        company ??= new OwnCompany { Name = request.Name.Trim(), TaxNumber = taxNumber };
        company.Name = request.Name.Trim();
        company.ShortName = Optional(request.ShortName);
        company.TaxNumber = taxNumber;
        company.PostalCode = Optional(request.PostalCode);
        company.City = Optional(request.City);
        company.AddressLine = Optional(request.AddressLine);
        company.Notes = Optional(request.Notes);
        company.IsActive = request.IsActive;
        if (isNew) db.OwnCompanies.Add(company);
        else db.Entry(company).Property(x => x.RowVersion).OriginalValue = request.RowVersion!.ToArray();
        await db.SaveChangesAsync(cancellationToken);
        return Response<OwnCompanyDetails>.Success(ToDetails(company), "A cég adatait mentettük.");
    }, cancellationToken);

    public Task<Response<OwnCompanyDetails>> SetActiveAsync(int id, bool isActive, byte[] rowVersion,
        CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        if (id <= 0 || rowVersion is not { Length: 8 })
            return Response<OwnCompanyDetails>.Invalid([new("RowVersion", "Töltsd újra a cég adatait.")]);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var company = await db.OwnCompanies.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (company is null) return Missing();
        if (!company.RowVersion.SequenceEqual(rowVersion)) return Conflict();
        company.IsActive = isActive;
        db.Entry(company).Property(x => x.RowVersion).OriginalValue = rowVersion.ToArray();
        await db.SaveChangesAsync(cancellationToken);
        return Response<OwnCompanyDetails>.Success(ToDetails(company), isActive ? "A céget újraaktiváltuk." : "A céget inaktiváltuk.");
    }, cancellationToken);

    private static List<ValidationError> Validate(SaveOwnCompanyRequest request)
    {
        var errors = new List<ValidationError>();
        void Text(string field, string? value, int maximum, bool required = false)
        {
            if (required && string.IsNullOrWhiteSpace(value)) errors.Add(new(field, field == "Name" ? "A cégnév kötelező." : "Az adószám / azonosító kötelező."));
            if (value?.Trim().Length > maximum) errors.Add(new(field, $"A mező legfeljebb {maximum} karakter lehet."));
        }
        Text("Name", request.Name, 200, true);
        Text("ShortName", request.ShortName, 50);
        if (!request.GenerateInternalIdentifier) Text("TaxNumber", request.TaxNumber, 20, true);
        Text("PostalCode", request.PostalCode, 10);
        Text("City", request.City, 100);
        Text("AddressLine", request.AddressLine, 200);
        Text("Notes", request.Notes, 2000);
        if (request.Id.HasValue && (request.Id <= 0 || request.RowVersion is not { Length: 8 }))
            errors.Add(new("RowVersion", "Töltsd újra a szerkesztendő cég adatait."));
        return errors;
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static OwnCompanyDetails ToDetails(OwnCompany x) => new(x.Id, x.Name, x.ShortName, x.TaxNumber,
        x.PostalCode, x.City, x.AddressLine, x.Notes, x.IsActive, x.RowVersion.ToArray());
    private static Response<OwnCompanyDetails> Missing() => Response<OwnCompanyDetails>.Failure(ErrorCode.NotFound, "A cég már nem található.");
    private static Response<OwnCompanyDetails> Conflict() => Response<OwnCompanyDetails>.Failure(ErrorCode.ConcurrencyConflict,
        "A cég adatait időközben másik kliens módosította. Frissítsd az adatokat, majd ismételd meg a módosítást.");

    private static async Task<Response<T>> RunAsync<T>(Func<Task<Response<T>>> action, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try { return await action(); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (DbUpdateConcurrencyException)
        {
            return Response<T>.Failure(ErrorCode.ConcurrencyConflict, "A cég adatait másik kliens módosította. Frissítsd az adatokat.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return Response<T>.Failure(ErrorCode.Duplicate, "Ez az adószám / azonosító már szerepel egy másik saját cégnél.");
        }
        catch (Exception ex) when (ex is SqlException or DbUpdateException or TimeoutException)
        {
            Trace.TraceError("Own company database operation failed ({0}).", ex.GetType().Name);
            return Response<T>.Failure(ErrorCode.DatabaseUnavailable, "Az adatbázis nem érhető el vagy nincs előkészítve. Próbáld újra a műveletet.");
        }
        catch (Exception ex)
        {
            Trace.TraceError("Own company operation failed ({0}).", ex.GetType().Name);
            return Response<T>.Failure(ErrorCode.Unexpected, "Váratlan hiba történt. Próbáld újra a műveletet.");
        }
    }
}
