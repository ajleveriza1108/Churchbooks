using ChurchBooks.Core.Finance;

namespace ChurchBooks.Accounting.Banking;

public sealed record BankDeposit
{
    public Guid Id { get; }
    public Guid BankAccountId { get; }
    public DateOnly DepositDate { get; }
    public CurrencyCode Currency { get; }
    public IReadOnlyList<DepositContributionAllocation> Contributions { get; }
    public decimal TotalAmount { get; }
    public string Reference { get; }
    public string Memo { get; }
    public Guid JournalEntryId { get; }
    public BankDepositStatus Status { get; }
    public DateTimeOffset CreatedUtc { get; }
    public DateTimeOffset? PostedUtc { get; }

    public BankDeposit(Guid id, Guid bankAccountId, DateOnly depositDate, CurrencyCode currency,
        IEnumerable<DepositContributionAllocation> contributions, string reference, string memo, Guid journalEntryId,
        BankDepositStatus status = BankDepositStatus.Draft, DateTimeOffset? createdUtc = null, DateTimeOffset? postedUtc = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Deposit ID cannot be empty.", nameof(id));
        if (bankAccountId == Guid.Empty) throw new ArgumentException("Bank account ID cannot be empty.", nameof(bankAccountId));
        if (journalEntryId == Guid.Empty) throw new ArgumentException("Journal entry ID cannot be empty.", nameof(journalEntryId));
        ArgumentNullException.ThrowIfNull(contributions);
        var items = contributions.ToArray();
        if (items.Length == 0) throw new ArgumentException("A deposit must include at least one contribution.", nameof(contributions));
        if (items.Select(x => x.ContributionId).Distinct().Count() != items.Length) throw new ArgumentException("A contribution cannot appear twice in one deposit.", nameof(contributions));
        Id=id; BankAccountId=bankAccountId; DepositDate=depositDate; Currency=currency; Contributions=items;
        TotalAmount=items.Sum(x=>x.Amount); Reference=reference?.Trim() ?? string.Empty; Memo=memo?.Trim() ?? string.Empty;
        JournalEntryId=journalEntryId; Status=status; CreatedUtc=createdUtc ?? DateTimeOffset.UtcNow; PostedUtc=postedUtc;
    }
}
