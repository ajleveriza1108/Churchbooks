namespace ChurchBooks.Accounting.Offerings;

public sealed record ContributionLine
{
    public Guid Id { get; }
    public Guid GivingCategoryId { get; }
    public Guid FundId { get; }
    public decimal Amount { get; }

    public ContributionLine(Guid id, Guid givingCategoryId, Guid fundId, decimal amount)
    {
        if (id == Guid.Empty) throw new ArgumentException("Contribution line ID cannot be empty.", nameof(id));
        if (givingCategoryId == Guid.Empty) throw new ArgumentException("Giving category ID cannot be empty.", nameof(givingCategoryId));
        if (fundId == Guid.Empty) throw new ArgumentException("Fund ID cannot be empty.", nameof(fundId));
        if (amount <= 0m) throw new ArgumentOutOfRangeException(nameof(amount), "Contribution amount must be greater than zero.");
        if (decimal.Round(amount, 2, MidpointRounding.ToEven) != amount)
            throw new ArgumentException("Contribution amount cannot contain more than two decimal places.", nameof(amount));

        Id = id;
        GivingCategoryId = givingCategoryId;
        FundId = fundId;
        Amount = amount;
    }
}
