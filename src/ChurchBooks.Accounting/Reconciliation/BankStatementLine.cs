namespace ChurchBooks.Accounting.Reconciliation;

public sealed record BankStatementLine
{
    public Guid Id { get; }
    public Guid BankAccountId { get; }
    public Guid ImportSessionId { get; }
    public DateOnly TransactionDate { get; }
    public decimal Amount { get; }
    public string Description { get; }
    public string Reference { get; }
    public string Fingerprint { get; }
    public int SourceRowNumber { get; }
    public DateTimeOffset CreatedUtc { get; }

    public BankStatementLine(
        Guid id,
        Guid bankAccountId,
        Guid importSessionId,
        DateOnly transactionDate,
        decimal amount,
        string description,
        string reference,
        string fingerprint,
        int sourceRowNumber,
        DateTimeOffset? createdUtc = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Statement line ID is required.", nameof(id));
        if (bankAccountId == Guid.Empty) throw new ArgumentException("Bank account ID is required.", nameof(bankAccountId));
        if (importSessionId == Guid.Empty) throw new ArgumentException("Import session ID is required.", nameof(importSessionId));
        if (amount == 0m) throw new ArgumentOutOfRangeException(nameof(amount), "Statement amount cannot be zero.");
        if (sourceRowNumber < 2) throw new ArgumentOutOfRangeException(nameof(sourceRowNumber));
        var normalizedFingerprint = fingerprint?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedFingerprint)) throw new ArgumentException("Fingerprint is required.", nameof(fingerprint));

        Id = id;
        BankAccountId = bankAccountId;
        ImportSessionId = importSessionId;
        TransactionDate = transactionDate;
        Amount = amount;
        Description = description?.Trim() ?? string.Empty;
        Reference = reference?.Trim() ?? string.Empty;
        Fingerprint = normalizedFingerprint;
        SourceRowNumber = sourceRowNumber;
        CreatedUtc = createdUtc ?? DateTimeOffset.UtcNow;
    }
}
