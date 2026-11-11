using TFM_ACCOUNTING.Business.Common;

namespace TFM_ACCOUNTING.Business.Settings;

public sealed record BehaviorOption(string Key, string Name);
public sealed record SettingsGroup(string Key, string Name, IReadOnlyList<BehaviorOption> Behaviors)
{
    public override string ToString() => Name;
}

public static class SettingsCatalog
{
    public static IReadOnlyList<SettingsGroup> Groups { get; } = Array.AsReadOnly(new[]
    {
        new SettingsGroup("cost-categories", "Költségkategóriák", Array.Empty<BehaviorOption>()),
        new SettingsGroup("partner-types", "Partnertípusok", Array.Empty<BehaviorOption>()),
        new SettingsGroup("obligation-types", "Kötelezettségtípusok", Array.AsReadOnly(new[]
        {
            new BehaviorOption("general", "Általános költség"),
            new BehaviorOption("invoice", "Számlaadatokkal"),
            new BehaviorOption("period", "Kötelező havi időszakkal (bér, járulék)"),
            new BehaviorOption("period-invoice", "Számlaadatokkal és kötelező havi időszakkal")
        })),
        new SettingsGroup("invoice-types", "Számlatípusok", Array.AsReadOnly(new[]
        {
            new BehaviorOption("standard", "Eredeti számlára hivatkozás nélkül"),
            new BehaviorOption("correction", "Eredeti számlára kötelező hivatkozással")
        })),
        new SettingsGroup("payment-methods", "Fizetési módok", Array.Empty<BehaviorOption>()),
        new SettingsGroup("settlement-types", "Rendezési típusok", Array.AsReadOnly(new[]
        {
            new BehaviorOption("money", "Tényleges pénzmozgás"),
            new BehaviorOption("offset", "Jóváírás beszámítása")
        }))
    });
}

public sealed record SettingItem(int Id, string Name, string? Notes, bool IsActive,
    string? Code, string? BehaviorKey, byte[] RowVersion);

public sealed record SaveSettingRequest(int? Id, string Name, string? Notes, bool IsActive,
    string? BehaviorKey = null, byte[]? RowVersion = null);

public interface ISettingsService
{
    Task<Response<IReadOnlyList<SettingItem>>> ListAsync(string? search = null, bool includeInactive = true,
        CancellationToken cancellationToken = default);
    Task<Response<SettingItem>> SaveAsync(SaveSettingRequest request, CancellationToken cancellationToken = default);
    Task<Response<SettingItem>> SetActiveAsync(int id, bool isActive, byte[] rowVersion,
        CancellationToken cancellationToken = default);
}
