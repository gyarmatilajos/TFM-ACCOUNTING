using TFM_ACCOUNTING.Business.Common;

namespace TFM_ACCOUNTING.Business.Obligations;

public sealed record ObligationOption(int Id, string Name, bool IsActive, bool RequiresPeriod = false,
    bool HasInvoiceDetails = false, bool RequiresOriginal = false)
{
    public string DisplayName => IsActive ? Name : Name + " (inaktív)";
}
public sealed record ObligationOptions(IReadOnlyList<ObligationOption> Companies, IReadOnlyList<ObligationOption> Partners,
    IReadOnlyList<ObligationOption> Types, IReadOnlyList<ObligationOption> Categories,
    IReadOnlyList<ObligationOption> PaymentMethods, IReadOnlyList<ObligationOption> InvoiceTypes);
public sealed record InvoiceInput(string Number, int TypeId, DateOnly IssueDate, DateOnly PerformanceDate,
    decimal Net, decimal Vat, byte[]? Version = null);
public sealed record ObligationInput(int CompanyId, int PartnerId, int TypeId, int? CategoryId,
    string? Description, int? Year, int? Month, DateOnly DueDate, decimal Amount,
    int? PaymentMethodId, int? OriginalId, string? Notes, InvoiceInput? Invoice, byte[]? Version = null);
public sealed record ObligationDetails(int Id, ObligationInput Input, bool Archived,
    IReadOnlyList<SettlementSummary> Settlements, IReadOnlyList<AttachmentSummary> Attachments);
public sealed record SettlementSummary(DateOnly Date, decimal Amount, string Type, string? Method);
public sealed record AttachmentSummary(string Name, long Size, string? Notes);
public sealed record ObligationRow(int Id, string Company, string Partner, string Type, string? Description,
    string? Number, DateOnly DueDate, decimal Amount, decimal Settled, bool Archived, bool HasAttachment)
{
    public decimal Balance => Amount - Settled;
    public bool IsOverdue => Balance > 0 && DueDate < DateOnly.FromDateTime(DateTime.Today);
    public string Status => Balance == 0 ? "Rendezett" : Settled == 0 ? "Nyitott" : "Részben rendezett";
}
public sealed record ObligationQuery(string? Search = null, int? CompanyId = null, int? PartnerId = null,
    int? TypeId = null, int? CategoryId = null, bool? Archived = false, bool? HasAttachment = null,
    string? Status = null, string? DateField = null, DateOnly? From = null, DateOnly? To = null,
    decimal? Min = null, decimal? Max = null, int Page = 1, bool NoCategory = false, string? DueState = null,
    string? QuickType = null);
public sealed record ObligationPage(PageResult<ObligationRow> Page, decimal Payable, decimal Credit,
    decimal Overdue, decimal DueSoon, decimal PaidThisMonth);

