namespace TFM_ACCOUNTING.Data.Entities;

public sealed class OwnCompany : IAuditable
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? ShortName { get; set; }
    public required string TaxNumber { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? AddressLine { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}


