namespace TFM_ACCOUNTING.Data.Entities;

public sealed class Obligation : IAuditable
{
    public int Id { get; set; }
    public int OwnCompanyId { get; set; }
    public OwnCompany OwnCompany { get; set; } = null!;
    public int PartnerId { get; set; }
    public Partner Partner { get; set; } = null!;
    public int ObligationTypeId { get; set; }
    public ObligationType ObligationType { get; set; } = null!;
    public int? CategoryId { get; set; }
    public CostCategory? Category { get; set; }
    public required string Description { get; set; }
    public int? PeriodYear { get; set; }
    public int? PeriodMonth { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal Amount { get; set; }
    public int? PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public int? OriginalObligationId { get; set; }
    public Obligation? OriginalObligation { get; set; }
    public string? Notes { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public InvoiceDetails? InvoiceDetails { get; set; }
    public ICollection<PaymentEntry> PaymentEntries { get; set; } = new List<PaymentEntry>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}


