namespace TFM_ACCOUNTING.Data.Entities;

public interface ISettingsEntry : IAuditable
{
    int Id { get; set; }
    string Name { get; set; }
    string? Notes { get; set; }
    bool IsActive { get; set; }
    byte[] RowVersion { get; set; }
}

public interface ILookupEntry : ISettingsEntry
{
    string Code { get; set; }
}

