namespace TFM_ACCOUNTING.Data.Entities;

public sealed class InvoiceDetails
{
    public int ObligationId { get; set; }
    public Obligation Obligation { get; set; } = null!;
    // Repeated keys are constrained to the parent and allow a database-level unique invoice number.
    public int OwnCompanyId { get; set; }
    public int PartnerId { get; set; }
    public required string InvoiceNumber { get; set; }
    public int InvoiceTypeId { get; set; }
    public InvoiceType InvoiceType { get; set; } = null!;
    public DateOnly IssueDate { get; set; }
    public DateOnly PerformanceDate { get; set; }
    public decimal NetAmount { get; set; }
    public decimal VatAmount { get; set; }
    public byte[] RowVersion { get; set; } = [];
}


