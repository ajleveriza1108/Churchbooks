using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.Funds;

namespace ChurchBooks.App.Models;

public sealed class ExpenseLineDraft
{
    public Guid Id { get; } = Guid.NewGuid();
    public Account ExpenseAccount { get; }
    public Fund Fund { get; }
    public string Description { get; }
    public decimal Amount { get; }

    public ExpenseLineDraft(Account expenseAccount, Fund fund, decimal amount, string? description = null)
    {
        ExpenseAccount = expenseAccount ?? throw new ArgumentNullException(nameof(expenseAccount));
        Fund = fund ?? throw new ArgumentNullException(nameof(fund));
        if (amount <= 0m) throw new ArgumentOutOfRangeException(nameof(amount));
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Description = description?.Trim() ?? string.Empty;
    }

    public string AccountDisplay => $"{ExpenseAccount.Code} - {ExpenseAccount.Name}";
    public string FundDisplay => Fund.Name;
}
