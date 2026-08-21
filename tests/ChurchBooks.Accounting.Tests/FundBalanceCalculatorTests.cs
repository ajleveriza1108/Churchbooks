using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.Funds;
using Xunit;

namespace ChurchBooks.Accounting.Tests;

public sealed class FundBalanceCalculatorTests
{
    [Fact]
    public void Calculate_UsesAssetMinusLiabilityMovementWithoutDoubleCountingActivityAccounts()
    {
        var fundId = Guid.NewGuid();
        var activity = new[]
        {
            Line(fundId, AccountType.Asset, 100m, 0m),
            Line(fundId, AccountType.Liability, 0m, 20m),
            Line(fundId, AccountType.Income, 0m, 100m),
            Line(fundId, AccountType.Expense, 20m, 0m)
        };

        var balance = FundBalanceCalculator.Calculate(activity);

        Assert.Equal(80m, balance);
    }

    [Fact]
    public void FundActivityLine_IncomeAndExpenseHaveZeroDirectNetAssetImpact()
    {
        var fundId = Guid.NewGuid();

        Assert.Equal(0m, Line(fundId, AccountType.Income, 0m, 50m).NetAssetImpact);
        Assert.Equal(0m, Line(fundId, AccountType.Expense, 50m, 0m).NetAssetImpact);
    }

    private static FundActivityLine Line(Guid fundId, AccountType type, decimal debit, decimal credit) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "JE-FUND",
        new DateOnly(2026, 8, 19),
        fundId,
        Guid.NewGuid(),
        type,
        debit,
        credit,
        "Fund activity",
        string.Empty,
        string.Empty);
}
