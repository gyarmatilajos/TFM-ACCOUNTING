using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TFM_ACCOUNTING.Data;
using TFM_ACCOUNTING.Data.Entities;

namespace TFM_ACCOUNTING.Business.Settings;

public sealed class SettingsServiceFactory(IDbContextFactory<AccountingDbContext> contextFactory, Action<Exception>? logError = null)
{
    public static SettingsServiceFactory FromConnectionString(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return new(AccountingDatabase.CreateFactory(connectionString));
    }

    public ISettingsService Create(string groupKey) => groupKey switch
    {
        "cost-categories" => new SettingsService<CostCategory>(contextFactory, groupKey, logError),
        "partner-types" => new SettingsService<PartnerType>(contextFactory, groupKey, logError),
        "obligation-types" => new SettingsService<ObligationType>(contextFactory, groupKey, logError),
        "invoice-types" => new SettingsService<InvoiceType>(contextFactory, groupKey, logError),
        "payment-methods" => new SettingsService<PaymentMethod>(contextFactory, groupKey, logError),
        "settlement-types" => new SettingsService<SettlementType>(contextFactory, groupKey, logError),
        _ => throw new ArgumentOutOfRangeException(nameof(groupKey), "Ismeretlen beállításcsoport.")
    };
}
