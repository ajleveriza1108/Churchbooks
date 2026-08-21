namespace ChurchBooks.Accounting.Expenses;

public sealed record DirectExpenseLine
{
    public Guid Id { get; }
    public Guid ExpenseAccountId { get; }
    public Guid FundId { get; }
    public string Description { get; }
    public decimal Amount { get; }

    public DirectExpenseLine(Guid id, Guid expenseAccountId, Guid fundId, decimal amount, string? description = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Expense line ID cannot be empty.", nameof(id));
        if (expenseAccountId == Guid.Empty) throw new ArgumentException("Expense account ID cannot be empty.", nameof(expenseAccountId));
        if (fundId == Guid.Empty) throw new ArgumentException("Fund ID cannot be empty.", nameof(fundId));
        if (amount <= 0m) throw new ArgumentOutOfRangeException(nameof(amount), "Expense amount must be greater than zero.");

        Id = id;
        ExpenseAccountId = expenseAccountId;
        FundId = fundId;
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Description = description?.Trim() ?? string.Empty;
    }
}
