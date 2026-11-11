namespace TFM_ACCOUNTING.Data.Entities;

public sealed class ObligationType : ILookupEntry
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public bool RequiresPeriod { get; set; }
    public bool HasInvoiceDetails { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

