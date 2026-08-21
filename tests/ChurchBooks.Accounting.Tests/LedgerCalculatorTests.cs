using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.GeneralLedger;
using Xunit;

namespace ChurchBooks.Accounting.Tests;

public sealed class LedgerCalculatorTests
{
    [Fact]
    public void Calculate_DebitNormalAccount_UsesDebitsMinusCredits()
    {
        var account = new Account(Guid.NewGuid(), "1000", "Cash", AccountType.Asset);
        var lines = new[]
        {
            new LedgerLine(Guid.NewGuid(), "JE-1", new DateOnly(2026, 8, 1), account.Id, 1000m, 0m, "Deposit", string.Empty, string.Empty),
            new LedgerLine(Guid.NewGuid(), "JE-2", new DateOnly(2026, 8, 2), account.Id, 0m, 250m, "Payment", string.Empty, string.Empty)
        };

        var result = LedgerCalculator.Calculate(account, lines);

        Assert.Equal(1000m, result.TotalDebit);
        Assert.Equal(250m, result.TotalCredit);
        Assert.Equal(750m, result.Balance);
    }

    [Fact]
    public void Calculate_CreditNormalAccount_UsesCreditsMinusDebits()
    {
        var account = new Account(Guid.NewGuid(), "4000", "Contribution Income", AccountType.Income);
        var lines = new[]
        {
            new LedgerLine(Guid.NewGuid(), "JE-1", new DateOnly(2026, 8, 1), account.Id, 0m, 1000m, "Giving", string.Empty, string.Empty),
            new LedgerLine(Guid.NewGuid(), "JE-2", new DateOnly(2026, 8, 2), account.Id, 100m, 0m, "Correction", string.Empty, string.Empty)
        };

        var result = LedgerCalculator.Calculate(account, lines);

        Assert.Equal(900m, result.Balance);
    }
}
