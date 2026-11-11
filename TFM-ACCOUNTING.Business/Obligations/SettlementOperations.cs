using Microsoft.EntityFrameworkCore;
using TFM_ACCOUNTING.Business.Common;
using TFM_ACCOUNTING.Data;
using TFM_ACCOUNTING.Data.Entities;

namespace TFM_ACCOUNTING.Business.Obligations;

public sealed partial class ObligationService
{
    public Task<Response<CashMovementReport>> CashMovementsAsync(DateOnly? from, DateOnly? to) => Run(async db =>
    {
        if (from > to)
            return Response<CashMovementReport>.Invalid([new("Date", "A kezdő dátum nem lehet későbbi a záró dátumnál.")]);
        var query = db.PaymentEntries.AsNoTracking().Where(x => x.SettlementGroupId == null && !x.SettlementType.IsCreditOffset);
        if (from is DateOnly start) query = query.Where(x => x.SettlementDate >= start);
        if (to is DateOnly end) query = query.Where(x => x.SettlementDate <= end);
        var rows = await query.OrderByDescending(x => x.SettlementDate).ThenByDescending(x => x.Id)
            .Select(x => new CashMovementRow(x.SettlementDate, x.Obligation.OwnCompany.ShortName ?? x.Obligation.OwnCompany.Name,
                x.Obligation.Partner.ShortName ?? x.Obligation.Partner.Name,
                x.Obligation.InvoiceDetails != null ? x.Obligation.InvoiceDetails.InvoiceNumber : x.Obligation.Description ?? ("Tétel #" + x.ObligationId),
                x.Amount, x.PaymentMethod!.Name, x.Reference)).ToListAsync();
        return Response<CashMovementReport>.Success(new(rows, rows.Where(x => x.Amount > 0).Sum(x => x.Amount),
            -rows.Where(x => x.Amount < 0).Sum(x => x.Amount)));
    });

    public Task<Response<SettlementScreen>> GetSettlementsAsync(int obligationId) => Run(async db =>
    {
        var item = await db.Obligations.AsNoTracking().Include(x => x.InvoiceDetails).SingleOrDefaultAsync(x => x.Id == obligationId);
        if (item is null) return Missing<SettlementScreen>();

        var entries = await db.PaymentEntries.AsNoTracking().Where(x => x.ObligationId == obligationId)
            .Include(x => x.SettlementType).Include(x => x.PaymentMethod)
            .OrderByDescending(x => x.SettlementDate).ThenByDescending(x => x.Id).ToListAsync();
        var groups = entries.Where(x => x.SettlementGroupId.HasValue).Select(x => x.SettlementGroupId).ToArray();
        var counterparts = groups.Length == 0 ? [] : await db.PaymentEntries.AsNoTracking()
            .Where(x => x.ObligationId != obligationId && groups.Contains(x.SettlementGroupId))
            .Include(x => x.Obligation).ThenInclude(x => x.InvoiceDetails).ToListAsync();
        var rows = entries.Select(entry =>
        {
            var other = counterparts.FirstOrDefault(x => x.SettlementGroupId == entry.SettlementGroupId);
            return new SettlementRow(new(entry.Id, entry.RowVersion, other?.Id, other?.RowVersion),
                entry.SettlementDate, entry.Amount, entry.SettlementTypeId, entry.SettlementType.Name, entry.SettlementGroupId.HasValue,
                entry.PaymentMethodId, entry.PaymentMethod?.Name, entry.Reference, entry.Notes, other is null ? null : ItemName(other.Obligation));
        }).ToArray();

        var types = await db.SettlementTypes.OrderBy(x => x.Name)
            .Select(x => new SettlementTypeOption(x.Id, x.Name, x.IsActive, x.IsCreditOffset)).ToListAsync();
        var methods = await db.PaymentMethods.OrderBy(x => x.Name)
            .Select(x => new ObligationOption(x.Id, x.Name, x.IsActive, false, false, false)).ToListAsync();
        var positive = item.Amount > 0;
        var candidates = await db.Obligations.AsNoTracking()
            .Where(x => x.OwnCompanyId == item.OwnCompanyId && x.PartnerId == item.PartnerId &&
                (positive ? x.Amount < 0 : x.Amount > 0))
            .Select(x => new
            {
                x.Id,
                Name = x.InvoiceDetails != null ? x.InvoiceDetails.InvoiceNumber : x.Description,
                x.DueDate,
                Balance = x.Amount - (x.PaymentEntries.Sum(p => (decimal?)p.Amount) ?? 0m)
            })
            .Where(x => positive ? x.Balance < 0 : x.Balance > 0)
            .OrderBy(x => x.DueDate).ThenBy(x => x.Id)
            .Select(x => new OffsetCandidate(x.Id, x.Name ?? ("Tétel #" + x.Id), x.Balance)).ToListAsync();

        return Response<SettlementScreen>.Success(new(ItemName(item), item.Amount - entries.Sum(x => x.Amount), rows, types, methods, candidates));
    });

    public Task<Response<int>> SaveSettlementAsync(int obligationId, SettlementInput input) => Write(async db =>
    {
        if (input.Date == default) return InvalidSettlement("Date", "Add meg a rendezés dátumát.");
        if (input.Reference?.Trim().Length > 200 || input.Notes?.Trim().Length > 2000)
            return InvalidSettlement("Notes", "A hivatkozás legfeljebb 200, a megjegyzés legfeljebb 2000 karakteres lehet.");

        var item = await db.Obligations.FindAsync(obligationId);
        if (item is null) return Missing<int>();

        PaymentEntry? entry = null;
        PaymentEntry? otherEntry = null;
        if (input.Existing is { } version)
        {
            (entry, otherEntry) = await FindSettlementAsync(db, obligationId, version);
            if (entry is null) return Conflict<int>();
        }

        var offset = entry is null ? input.OtherObligationId.HasValue : entry.SettlementGroupId.HasValue;
        if (entry is not null && input.OtherObligationId.HasValue && input.OtherObligationId != otherEntry?.ObligationId)
            return InvalidSettlement("OtherObligationId", "Meglévő rendezésnél a kapcsolódó tétel nem változtatható.");

        var settlementType = await db.SettlementTypes.FindAsync(input.TypeId);
        if (settlementType is null || settlementType.IsCreditOffset != offset ||
            (!settlementType.IsActive && entry?.SettlementTypeId != settlementType.Id))
            return InvalidSettlement("TypeId", "Válassz megfelelő, aktív rendezési típust.");

        Obligation? other = null;
        if (offset)
        {
            if (input.MethodId.HasValue || input.FullBalance)
                return InvalidSettlement("MethodId", "Beszámításhoz add meg a beszámítandó összeget.");
            other = await db.Obligations.FindAsync(otherEntry?.ObligationId ?? input.OtherObligationId!.Value);
            if (other is null || other.OwnCompanyId != item.OwnCompanyId || other.PartnerId != item.PartnerId ||
                Math.Sign(other.Amount) == Math.Sign(item.Amount))
                return InvalidSettlement("OtherObligationId", "Azonos céghez és partnerhez tartozó, ellentétes előjelű tételt válassz.");
        }
        else
        {
            if (input.MethodId is not int methodId)
                return InvalidSettlement("MethodId", "Válassz fizetési módot.");
            var method = await db.PaymentMethods.FindAsync(methodId);
            if (method is null || (!method.IsActive && entry?.PaymentMethodId != methodId))
                return InvalidSettlement("MethodId", "Válassz aktív fizetési módot.");
        }

        var available = await AvailableAsync(db, item, entry?.Id);
        var amount = input.FullBalance ? available : input.Amount;
        if (amount <= 0 || !ValidAmount(amount))
            return InvalidSettlement("Amount", "Adj meg pozitív összeget, legfeljebb két tizedesjeggyel.");
        if (amount > available || (other is not null && amount > await AvailableAsync(db, other, otherEntry?.Id)))
            return InvalidSettlement("Amount", "Az összeg meghaladja az aktuálisan rendezhető hátralékot.");

        if (entry is null)
        {
            var group = offset ? Guid.NewGuid() : (Guid?)null;
            entry = new PaymentEntry { ObligationId = item.Id, SettlementGroupId = group };
            db.PaymentEntries.Add(entry);
            if (other is not null)
            {
                otherEntry = new PaymentEntry { ObligationId = other.Id, SettlementGroupId = group };
                db.PaymentEntries.Add(otherEntry);
            }
        }

        var signedAmount = amount * Math.Sign(item.Amount);
        ApplySettlement(entry, input, signedAmount);
        if (otherEntry is not null) ApplySettlement(otherEntry, input, -signedAmount);
        await db.SaveChangesAsync();
        return Response<int>.Success(entry.Id, offset ? "A jóváírást beszámítottuk." : "A pénzmozgást mentettük.");
    });

    public Task<Response<int>> DeleteSettlementAsync(int obligationId, SettlementVersion version) => Write(async db =>
    {
        var (entry, other) = await FindSettlementAsync(db, obligationId, version);
        if (entry is null) return Conflict<int>();
        db.PaymentEntries.Remove(entry);
        if (other is not null) db.PaymentEntries.Remove(other);
        await db.SaveChangesAsync();
        return Response<int>.Success(version.Id, "A rendezést visszavontuk.");
    });

    private static async Task<(PaymentEntry? Entry, PaymentEntry? Other)> FindSettlementAsync(
        AccountingDbContext db, int obligationId, SettlementVersion version)
    {
        var entry = await db.PaymentEntries.SingleOrDefaultAsync(x => x.Id == version.Id && x.ObligationId == obligationId);
        if (entry is null || !SameVersion(entry.RowVersion, version.RowVersion)) return (null, null);
        if (entry.SettlementGroupId is not Guid group) return (entry, null);

        var others = await db.PaymentEntries.Where(x => x.SettlementGroupId == group && x.Id != entry.Id).Take(2).ToListAsync();
        if (others.Count != 1) return (null, null);
        var other = others[0];
        if (other.Id != version.CounterpartId || !SameVersion(other.RowVersion, version.CounterpartVersion) || other.Amount != -entry.Amount)
            return (null, null);
        return (entry, other);
    }

    private static async Task<decimal> AvailableAsync(AccountingDbContext db, Obligation item, int? excludedEntry)
    {
        var settled = await db.PaymentEntries.Where(x => x.ObligationId == item.Id && x.Id != excludedEntry)
            .SumAsync(x => (decimal?)x.Amount) ?? 0m;
        return (item.Amount - settled) * Math.Sign(item.Amount);
    }

    private static void ApplySettlement(PaymentEntry entry, SettlementInput input, decimal amount)
    {
        entry.SettlementDate = input.Date;
        entry.Amount = amount;
        entry.SettlementTypeId = input.TypeId;
        entry.PaymentMethodId = input.MethodId;
        entry.Reference = string.IsNullOrWhiteSpace(input.Reference) ? null : input.Reference.Trim();
        entry.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
    }

    private static string ItemName(Obligation item) => item.InvoiceDetails?.InvoiceNumber ?? item.Description ?? $"Tétel #{item.Id}";

    private static Response<int> InvalidSettlement(string field, string message) => Response<int>.Invalid([new(field, message)]);
}
