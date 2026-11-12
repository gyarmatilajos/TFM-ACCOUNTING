using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TFM_ACCOUNTING.Business.Common;
using TFM_ACCOUNTING.Data;
using TFM_ACCOUNTING.Data.Entities;

namespace TFM_ACCOUNTING.Business.Partners;

public sealed class PartnerService(IDbContextFactory<AccountingDbContext> contextFactory) : IPartnerService
{
    public Task<Response<PageResult<PartnerListItem>>> ListAsync(PartnerQuery query,
        CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue || query.Search?.Trim().Length > 200 ||
            query.PartnerTypeId is <= 0 || query.SortBy is not ("Name" or "ShortName" or "TaxNumber" or "IsActive" or "PartnerType"))
            return Response<PageResult<PartnerListItem>>.Invalid([new("Query", "Érvénytelen keresés, rendezés vagy lapozás.")]);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = db.Partners.AsNoTracking();
        if (query.PartnerTypeId.HasValue) rows = rows.Where(x => x.PartnerTypeId == query.PartnerTypeId.Value);
        if (query.IsActive.HasValue) rows = rows.Where(x => x.IsActive == query.IsActive.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x => x.Name.Contains(search) || (x.ShortName != null && x.ShortName.Contains(search)) || x.TaxNumber.Contains(search));
        }
        var total = await rows.CountAsync(cancellationToken);
        IOrderedQueryable<Partner> sorted = (query.SortBy, query.Descending) switch
        {
            ("PartnerType", false) => rows.OrderBy(x => x.PartnerType.Name),
            ("PartnerType", true) => rows.OrderByDescending(x => x.PartnerType.Name),
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
            return Response<PageResult<PartnerListItem>>.Invalid([new("Page", "Túl nagy oldalszám.")]);
        var items = await sorted.ThenBy(x => x.Id).Skip((int)skip).Take(query.PageSize)
            .Select(x => new PartnerListItem(x.Id, x.Name, x.ShortName, x.TaxNumber, x.City, x.IsActive, x.PartnerTypeId, x.PartnerType.Name))
            .ToListAsync(cancellationToken);
        return Response<PageResult<PartnerListItem>>.Success(new(items, total, query.Page, query.PageSize));
    }, cancellationToken);

    public Task<Response<IReadOnlyList<PartnerTypeOption>>> ListTypesAsync(bool includeInactive = false,
        CancellationToken cancellationToken = default) => RunAsync<IReadOnlyList<PartnerTypeOption>>(async () =>
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var types = await db.PartnerTypes.AsNoTracking().Where(x => includeInactive || x.IsActive)
            .OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new PartnerTypeOption(x.Id, x.Name, x.IsActive)).ToListAsync(cancellationToken);
        return Response<IReadOnlyList<PartnerTypeOption>>.Success(types);
    }, cancellationToken);

    public Task<Response<PartnerDetails>> GetAsync(int id, CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var partner = await db.Partners.AsNoTracking().Include(x => x.PartnerType).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return partner is null ? Missing() : Response<PartnerDetails>.Success(ToDetails(partner));
    }, cancellationToken);

    public Task<Response<PartnerDetails>> SaveAsync(SavePartnerRequest request, CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        var errors = Validate(request);
        if (errors.Count > 0) return Response<PartnerDetails>.Invalid(errors);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var partner = request.Id.HasValue
            ? await db.Partners.Include(x => x.PartnerType).SingleOrDefaultAsync(x => x.Id == request.Id.Value, cancellationToken) : null;
        if (request.Id.HasValue && partner is null) return Missing();
        if (partner is not null && !partner.RowVersion.SequenceEqual(request.RowVersion!)) return Conflict();

        var type = await db.PartnerTypes.SingleOrDefaultAsync(x => x.Id == request.PartnerTypeId, cancellationToken);
        if (type is null || (!type.IsActive && partner?.PartnerTypeId != type.Id))
            return Response<PartnerDetails>.Invalid([new("PartnerTypeId", "Válassz létező, aktív partnertípust.")]);

        var taxNumber = request.GenerateInternalIdentifier
            ? (partner?.TaxNumber.StartsWith("BELSO-", StringComparison.OrdinalIgnoreCase) == true
                ? partner.TaxNumber : "BELSO-" + Guid.NewGuid().ToString("N")[..14].ToUpperInvariant())
            : request.TaxNumber!.Trim();
        if (await db.Partners.AnyAsync(x => x.TaxNumber == taxNumber && (!request.Id.HasValue || x.Id != request.Id.Value), cancellationToken))
            return Response<PartnerDetails>.Failure(ErrorCode.Duplicate, "Ez az adószám / azonosító már szerepel egy másik partnernél.");

        // An unchanged name has already been accepted for this record; self-edits need no repeat confirmation.
        if (!request.ConfirmDuplicateName && (partner is null || partner.Name != request.Name.Trim()))
        {
            var normalizedName = request.Name.Trim();
            var matches = await db.Partners.AsNoTracking()
                .Where(x => x.Name == normalizedName && (!request.Id.HasValue || x.Id != request.Id.Value))
                .OrderBy(x => x.Id).Select(x => new RelatedRecord(x.Id, x.Name, x.TaxNumber))
                .Take(10).ToListAsync(cancellationToken);
            if (matches.Count > 0)
                return Response<PartnerDetails>.ConfirmationRequired(
                    [new("PartnerNameMatch", "Már létezik ilyen nevű partner. Ellenőrizd, hogy ugyanarról a személyről vagy szervezetről van-e szó.", matches)],
                    "Azonos nevű partner található. Az új vagy módosított partner mentése megerősítést igényel.");
        }

        var isNew = partner is null;
        partner ??= new Partner { Name = request.Name.Trim(), TaxNumber = taxNumber };
        partner.Name = request.Name.Trim();
        partner.PartnerType = type;
        partner.PartnerTypeId = type.Id;
        partner.ShortName = Optional(request.ShortName);
        partner.TaxNumber = taxNumber;
        partner.BankAccountNumber = Optional(request.BankAccountNumber);
        partner.PostalCode = Optional(request.PostalCode);
        partner.City = Optional(request.City);
        partner.AddressLine = Optional(request.AddressLine);
        partner.Notes = Optional(request.Notes);
        partner.IsActive = request.IsActive;
        if (isNew) db.Partners.Add(partner);
        else db.Entry(partner).Property(x => x.RowVersion).OriginalValue = request.RowVersion!.ToArray();
        await db.SaveChangesAsync(cancellationToken);
        return Response<PartnerDetails>.Success(ToDetails(partner), "A partner adatait mentettük.");
    }, cancellationToken);

    public Task<Response<PartnerDetails>> SetActiveAsync(int id, bool isActive, byte[] rowVersion,
        CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        if (id <= 0 || rowVersion is not { Length: 8 })
            return Response<PartnerDetails>.Invalid([new("RowVersion", "Töltsd újra a partner adatait.")]);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var partner = await db.Partners.Include(x => x.PartnerType).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (partner is null) return Missing();
        if (!partner.RowVersion.SequenceEqual(rowVersion)) return Conflict();
        partner.IsActive = isActive;
        db.Entry(partner).Property(x => x.RowVersion).OriginalValue = rowVersion.ToArray();
        await db.SaveChangesAsync(cancellationToken);
        return Response<PartnerDetails>.Success(ToDetails(partner), isActive ? "A partnert újraaktiváltuk." : "A partnert inaktiváltuk.");
    }, cancellationToken);

    private static List<ValidationError> Validate(SavePartnerRequest request)
    {
        var errors = new List<ValidationError>();
        void Text(string field, string? value, int maximum, bool required = false)
        {
            if (required && string.IsNullOrWhiteSpace(value)) errors.Add(new(field, field == "Name" ? "A partnernév kötelező." : "Az adószám / azonosító kötelező."));
            if (value?.Trim().Length > maximum) errors.Add(new(field, $"A mező legfeljebb {maximum} karakter lehet."));
        }
        Text("Name", request.Name, 200, true);
        if (request.PartnerTypeId <= 0) errors.Add(new("PartnerTypeId", "A partnertípus kiválasztása kötelező."));
        Text("ShortName", request.ShortName, 50);
        if (!request.GenerateInternalIdentifier) Text("TaxNumber", request.TaxNumber, 20, true);
        Text(nameof(request.BankAccountNumber), request.BankAccountNumber, 50);
        Text("PostalCode", request.PostalCode, 10);
        Text("City", request.City, 100);
        Text("AddressLine", request.AddressLine, 200);
        Text("Notes", request.Notes, 2000);
        if (request.Id.HasValue && (request.Id <= 0 || request.RowVersion is not { Length: 8 }))
            errors.Add(new("RowVersion", "Töltsd újra a szerkesztendő partner adatait."));
        return errors;
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static PartnerDetails ToDetails(Partner x) => new(x.Id, x.Name, x.ShortName, x.TaxNumber,
        x.PostalCode, x.City, x.AddressLine, x.Notes, x.IsActive, x.RowVersion.ToArray(), x.PartnerTypeId, x.PartnerType.Name,
        x.BankAccountNumber);
    private static Response<PartnerDetails> Missing() => Response<PartnerDetails>.Failure(ErrorCode.NotFound, "A partner már nem található.");
    private static Response<PartnerDetails> Conflict() => Response<PartnerDetails>.Failure(ErrorCode.ConcurrencyConflict,
        "A partner adatait időközben másik kliens módosította. Frissítsd az adatokat, majd ismételd meg a módosítást.");

    private static async Task<Response<T>> RunAsync<T>(Func<Task<Response<T>>> action, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try { return await action(); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (DbUpdateConcurrencyException)
        {
            return Response<T>.Failure(ErrorCode.ConcurrencyConflict, "A partner adatait másik kliens módosította. Frissítsd az adatokat.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return Response<T>.Failure(ErrorCode.Duplicate, "Ez az adószám / azonosító már szerepel egy másik partnernél.");
        }
        catch (Exception ex) when (ex is SqlException or DbUpdateException or TimeoutException)
        {
            Trace.TraceError("Partner database operation failed ({0}).", ex.GetType().Name);
            return Response<T>.Failure(ErrorCode.DatabaseUnavailable, "Az adatbázis nem érhető el vagy nincs előkészítve. Próbáld újra a műveletet.");
        }
        catch (Exception ex)
        {
            Trace.TraceError("Partner operation failed ({0}).", ex.GetType().Name);
            return Response<T>.Failure(ErrorCode.Unexpected, "Váratlan hiba történt. Próbáld újra a műveletet.");
        }
    }
}



