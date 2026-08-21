using ChurchBooks.Core.Finance;

namespace ChurchBooks.Accounting.Banking;

public sealed record BankAccount
{
    public Guid Id { get; }
    public string Name { get; }
    public string InstitutionName { get; }
    public string AccountLastFour { get; }
    public CurrencyCode Currency { get; }
    public Guid LedgerAccountId { get; }
    public BankAccountStatus Status { get; }
    public DateTimeOffset CreatedUtc { get; }
    public DateTimeOffset? ArchivedUtc { get; }

    public BankAccount(Guid id, string name, string institutionName, string accountLastFour, CurrencyCode currency,
        Guid ledgerAccountId, BankAccountStatus status = BankAccountStatus.Active, DateTimeOffset? createdUtc = null, DateTimeOffset? archivedUtc = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Bank account ID cannot be empty.", nameof(id));
        if (ledgerAccountId == Guid.Empty) throw new ArgumentException("Ledger account ID cannot be empty.", nameof(ledgerAccountId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(institutionName);
        var lastFour = accountLastFour?.Trim() ?? string.Empty;
        if (lastFour.Length > 4 || lastFour.Any(ch => !char.IsDigit(ch))) throw new ArgumentException("Account last four must contain zero to four digits.", nameof(accountLastFour));
        Id = id; Name = name.Trim(); InstitutionName = institutionName.Trim(); AccountLastFour = lastFour; Currency = currency;
        LedgerAccountId = ledgerAccountId; Status = status; CreatedUtc = createdUtc ?? DateTimeOffset.UtcNow; ArchivedUtc = archivedUtc;
    }
}
