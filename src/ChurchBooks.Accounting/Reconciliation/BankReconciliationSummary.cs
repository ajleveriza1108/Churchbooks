namespace ChurchBooks.Accounting.Reconciliation;

public sealed record BankReconciliationSummary(
    BankReconciliation Reconciliation,
    decimal BookEndingBalance,
    decimal OutstandingBookAmount,
    decimal ExpectedStatementBalance,
    decimal Difference,
    IReadOnlyList<BankStatementLine> UnmatchedStatementLines,
    IReadOnlyList<ReconciliationBookItem> UnmatchedBookItems,
    IReadOnlyList<ReconciliationMatchGroup> MatchGroups)
{
    public bool CanComplete =>
        Difference == 0m &&
        UnmatchedStatementLines.Count == 0;
}
