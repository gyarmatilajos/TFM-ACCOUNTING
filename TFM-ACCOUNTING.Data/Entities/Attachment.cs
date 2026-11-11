namespace TFM_ACCOUNTING.Data.Entities;

public sealed class Attachment : IAuditable
{
    public int Id { get; set; }
    public int ObligationId { get; set; }
    public Obligation Obligation { get; set; } = null!;
    public required string OriginalFileName { get; set; }
    public required string RelativePath { get; set; }
    public required string ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}


