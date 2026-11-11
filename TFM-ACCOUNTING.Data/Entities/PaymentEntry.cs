namespace TFM_ACCOUNTING.Data.Entities;

public sealed class PaymentEntry : IAuditable
{
    public int Id { get; set; }
    public int ObligationId { get; set; }
    public Obligation Obligation { get; set; } = null!;
    public int SettlementTypeId { get; set; }
    public SettlementType SettlementType { get; set; } = null!;
    public DateOnly SettlementDate { get; set; }
    public decimal Amount { get; set; }
    public int? PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? Reference { get; set; }
    public Guid? SettlementGroupId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}


