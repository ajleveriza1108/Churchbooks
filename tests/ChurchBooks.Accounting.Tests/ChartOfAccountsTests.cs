using ChurchBooks.Accounting.ChartOfAccounts;
using Xunit;

namespace ChurchBooks.Accounting.Tests;

public sealed class ChartOfAccountsTests
{
    [Theory]
    [InlineData(AccountType.Asset, AccountNormalBalance.Debit)]
    [InlineData(AccountType.Expense, AccountNormalBalance.Debit)]
    [InlineData(AccountType.Liability, AccountNormalBalance.Credit)]
    [InlineData(AccountType.Equity, AccountNormalBalance.Credit)]
    [InlineData(AccountType.Income, AccountNormalBalance.Credit)]
    public void Account_NormalBalance_FollowsDoubleEntryConvention(AccountType type, AccountNormalBalance expected)
    {
        var account = new Account(Guid.NewGuid(), "1000", "Test", type);
        Assert.Equal(expected, account.NormalBalance);
    }
}
