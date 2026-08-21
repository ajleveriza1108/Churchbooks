namespace ChurchBooks.Accounting.Banking;

public sealed record GivingCategoryIncomeMapping
{
    public Guid GivingCategoryId { get; }
    public Guid IncomeAccountId { get; }
    public GivingCategoryIncomeMapping(Guid givingCategoryId, Guid incomeAccountId)
    {
        if (givingCategoryId == Guid.Empty) throw new ArgumentException("Giving category ID cannot be empty.", nameof(givingCategoryId));
        if (incomeAccountId == Guid.Empty) throw new ArgumentException("Income account ID cannot be empty.", nameof(incomeAccountId));
        GivingCategoryId = givingCategoryId; IncomeAccountId = incomeAccountId;
    }
}
