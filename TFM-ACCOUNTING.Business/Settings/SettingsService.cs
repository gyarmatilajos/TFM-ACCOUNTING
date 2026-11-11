using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TFM_ACCOUNTING.Business.Common;
using TFM_ACCOUNTING.Data;
using TFM_ACCOUNTING.Data.Entities;

namespace TFM_ACCOUNTING.Business.Settings;

internal sealed class SettingsService<T>(IDbContextFactory<AccountingDbContext> contextFactory,
    string groupKey, Action<Exception>? logError) : ISettingsService where T : class, ISettingsEntry, new()
{
    public Task<Response<IReadOnlyList<SettingItem>>> ListAsync(string? search = null, bool includeInactive = true,
        CancellationToken cancellationToken = default) => ExecuteAsync<IReadOnlyList<SettingItem>>(async () =>
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Set<T>().AsNoTracking();
        if (!includeInactive) query = query.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Name.Contains(term));
        }
        var rows = await query.OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        return Response<IReadOnlyList<SettingItem>>.Success(rows.Select(ToItem).ToArray());
    }, cancellationToken);

    public Task<Response<SettingItem>> SaveAsync(SaveSettingRequest request, CancellationToken cancellationToken = default)
        => ExecuteAsync(async () =>
    {
        var errors = Validate(request);
        if (errors.Count > 0) return Response<SettingItem>.Invalid(errors);
        var name = request.Name.Trim();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = request.Id.HasValue
            ? await db.Set<T>().SingleOrDefaultAsync(x => x.Id == request.Id.Value, cancellationToken)
            : null;
        if (request.Id.HasValue && existing is null)
            return Response<SettingItem>.Failure(ErrorCode.NotFound, "A bejegyzés már nem található.");
        if (existing is not null && !existing.RowVersion.SequenceEqual(request.RowVersion!))
            return Conflict();
        if (existing is not null && GetBehavior(existing) != request.BehaviorKey)
            return Response<SettingItem>.Invalid([new("BehaviorKey", "A működés létrehozás után nem változtatható. Hozz létre új típust.")]);
        if (await db.Set<T>().AnyAsync(x => x.Name == name && (!request.Id.HasValue || x.Id != request.Id.Value), cancellationToken))
            return Response<SettingItem>.Failure(ErrorCode.Duplicate, "Ilyen nevű bejegyzés már létezik ebben a csoportban.");

        var entity = existing ?? new T();
        entity.Name = name;
        entity.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        entity.IsActive = request.IsActive;
        if (existing is null)
        {
            if (entity is ILookupEntry lookup) lookup.Code = "CUSTOM_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
            ApplyBehavior(entity, request.BehaviorKey);
            db.Set<T>().Add(entity);
        }
        else db.Entry(entity).Property(x => x.RowVersion).OriginalValue = request.RowVersion!.ToArray();

        await db.SaveChangesAsync(cancellationToken);
        return Response<SettingItem>.Success(ToItem(entity), "A bejegyzést mentettük.");
    }, cancellationToken);

    public Task<Response<SettingItem>> SetActiveAsync(int id, bool isActive, byte[] rowVersion,
        CancellationToken cancellationToken = default) => ExecuteAsync(async () =>
    {
        if (id <= 0 || rowVersion is not { Length: 8 })
            return Response<SettingItem>.Invalid([new("RowVersion", "Érvényes azonosító és verzió szükséges. Töltsd újra a listát.")]);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Set<T>().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return Response<SettingItem>.Failure(ErrorCode.NotFound, "A bejegyzés már nem található.");
        if (!entity.RowVersion.SequenceEqual(rowVersion)) return Conflict();
        entity.IsActive = isActive;
        db.Entry(entity).Property(x => x.RowVersion).OriginalValue = rowVersion.ToArray();
        await db.SaveChangesAsync(cancellationToken);
        return Response<SettingItem>.Success(ToItem(entity), isActive ? "A bejegyzés aktív." : "A bejegyzés inaktív.");
    }, cancellationToken);

    private List<ValidationError> Validate(SaveSettingRequest request)
    {
        var errors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(request.Name)) errors.Add(new("Name", "A név kötelező."));
        else if (request.Name.Trim().Length > 100) errors.Add(new("Name", "A név legfeljebb 100 karakter lehet."));
        if (request.Notes?.Trim().Length > 2000) errors.Add(new("Notes", "A megjegyzés legfeljebb 2000 karakter lehet."));
        if (request.Id.HasValue && (request.Id <= 0 || request.RowVersion is not { Length: 8 }))
            errors.Add(new("RowVersion", "Töltsd újra a szerkesztendő bejegyzést."));
        var behaviors = SettingsCatalog.Groups.Single(x => x.Key == groupKey).Behaviors;
        if (behaviors.Count > 0 && !behaviors.Any(x => x.Key == request.BehaviorKey))
            errors.Add(new("BehaviorKey", "Válaszd ki a típus működését."));
        if (behaviors.Count == 0 && request.BehaviorKey is not null)
            errors.Add(new("BehaviorKey", "Ehhez a csoporthoz nem tartozik működésválasztás."));
        return errors;
    }

    private static string? GetBehavior(T entity) => entity switch
    {
        ObligationType x => (x.RequiresPeriod, x.HasInvoiceDetails) switch
        {
            (true, true) => "period-invoice", (true, false) => "period", (false, true) => "invoice", _ => "general"
        },
        InvoiceType x => x.RequiresOriginalInvoice ? "correction" : "standard",
        SettlementType x => x.IsCreditOffset ? "offset" : "money",
        _ => null
    };

    private static void ApplyBehavior(T entity, string? key)
    {
        if (entity is ObligationType obligation)
        {
            obligation.RequiresPeriod = key is "period" or "period-invoice";
            obligation.HasInvoiceDetails = key is "invoice" or "period-invoice";
        }
        if (entity is InvoiceType invoice) invoice.RequiresOriginalInvoice = key == "correction";
        if (entity is SettlementType settlement) settlement.IsCreditOffset = key == "offset";
    }

    private static SettingItem ToItem(T entity) => new(entity.Id, entity.Name, entity.Notes, entity.IsActive,
        (entity as ILookupEntry)?.Code, GetBehavior(entity), entity.RowVersion.ToArray());

    private static Response<SettingItem> Conflict() => Response<SettingItem>.Failure(ErrorCode.ConcurrencyConflict,
        "A bejegyzést időközben másik kliens módosította. Frissítsd a listát, majd ismételd meg a módosítást.");

    private async Task<Response<TResult>> ExecuteAsync<TResult>(Func<Task<Response<TResult>>> action, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try { return await action(); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (DbUpdateConcurrencyException)
        {
            return Response<TResult>.Failure(ErrorCode.ConcurrencyConflict,
                "A bejegyzést időközben másik kliens módosította. Frissítsd a listát.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return Response<TResult>.Failure(ErrorCode.Duplicate, "Ilyen nevű vagy kódú bejegyzés már létezik ebben a csoportban.");
        }
        catch (Exception ex) when (ex is SqlException or DbUpdateException or InvalidOperationException or TimeoutException)
        {
            Log(ex);
            return Response<TResult>.Failure(ErrorCode.DatabaseUnavailable,
                "Az adatbázis-művelet nem sikerült. Ellenőrizd a kapcsolatot és az adatbázis előkészítését, majd próbáld újra.");
        }
        catch (Exception ex)
        {
            Log(ex);
            return Response<TResult>.Failure(ErrorCode.Unexpected, "Váratlan hiba történt. Próbáld újra a műveletet.");
        }
    }

    private void Log(Exception exception)
    {
        if (logError is not null) logError(exception);
        else Trace.TraceError("Settings operation failed ({0}).", exception.GetType().Name);
    }
}
