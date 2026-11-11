namespace TFM_ACCOUNTING.Business.Obligations;

public sealed record SettlementVersion(int Id, byte[] RowVersion, int? CounterpartId = null, byte[]? CounterpartVersion = null);

public sealed record SettlementRow(SettlementVersion Version, DateOnly Date, decimal Amount, int TypeId, string Type,
    bool IsOffset, int? MethodId, string? Method, string? Reference, string? Notes, string? RelatedItem);

public sealed record SettlementTypeOption(int Id, string Name, bool IsActive, bool IsOffset)
{
    public string DisplayName => IsActive ? Name : Name + " (inaktív)";
}

public sealed record OffsetCandidate(int Id, string Name, decimal Balance)
{
    public string DisplayName => $"{Name}  ·  {Math.Abs(Balance):N2} Ft";
}

public sealed record SettlementScreen(string ItemName, decimal Balance, IReadOnlyList<SettlementRow> Rows,
    IReadOnlyList<SettlementTypeOption> Types, IReadOnlyList<ObligationOption> Methods, IReadOnlyList<OffsetCandidate> Candidates);

public sealed record SettlementInput(DateOnly Date, decimal Amount, int TypeId, int? MethodId, int? OtherObligationId,
    string? Reference, string? Notes, bool FullBalance = false, SettlementVersion? Existing = null);

public sealed record CashMovementRow(DateOnly Date, string Company, string Partner, string Item,
    decimal Amount, string Method, string? Reference);

public sealed record CashMovementReport(IReadOnlyList<CashMovementRow> Rows, decimal Paid, decimal Refunded);
