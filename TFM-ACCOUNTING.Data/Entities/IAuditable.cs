namespace TFM_ACCOUNTING.Data.Entities;

public interface IAuditable
{
    DateTime CreatedAtUtc { get; set; }
    DateTime UpdatedAtUtc { get; set; }
}


