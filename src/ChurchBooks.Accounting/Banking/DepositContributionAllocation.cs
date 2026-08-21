namespace ChurchBooks.Accounting.Banking;

public sealed record DepositContributionAllocation
{
    public Guid ContributionId { get; }
    public decimal Amount { get; }
    public DepositContributionAllocation(Guid contributionId, decimal amount)
    {
        if (contributionId == Guid.Empty) throw new ArgumentException("Contribution ID cannot be empty.", nameof(contributionId));
        if (amount <= 0m) throw new ArgumentOutOfRangeException(nameof(amount));
        ContributionId = contributionId; Amount = amount;
    }
}
