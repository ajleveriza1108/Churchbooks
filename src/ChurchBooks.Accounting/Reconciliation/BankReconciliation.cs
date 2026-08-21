namespace ChurchBooks.Accounting.Reconciliation;

public sealed record BankReconciliation
{
    public Guid Id { get; }
    public Guid BankAccountId { get; }
    public DateOnly StatementStartDate { get; }
    public DateOnly StatementEndDate { get; }
    public decimal StatementEndingBalance { get; }
    public BankReconciliationStatus Status { get; }
    public DateTimeOffset CreatedUtc { get; }
    public DateTimeOffset? CompletedUtc { get; }

    public BankReconciliation(
        Guid id,
        Guid bankAccountId,
        DateOnly statementStartDate,
        DateOnly statementEndDate,
        decimal statementEndingBalance,
        BankReconciliationStatus status = BankReconciliationStatus.Draft,
        DateTimeOffset? createdUtc = null,
        DateTimeOffset? completedUtc = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Reconciliation ID is required.", nameof(id));
        if (bankAccountId == Guid.Empty) throw new ArgumentException("Bank account ID is required.", nameof(bankAccountId));
        if (statementEndDate < statementStartDate) throw new ArgumentException("Statement end date cannot precede the start date.");

        Id = id;
        BankAccountId = bankAccountId;
        StatementStartDate = statementStartDate;
        StatementEndDate = statementEndDate;
        StatementEndingBalance = statementEndingBalance;
        Status = status;
        CreatedUtc = createdUtc ?? DateTimeOffset.UtcNow;
        CompletedUtc = completedUtc;
    }
}
