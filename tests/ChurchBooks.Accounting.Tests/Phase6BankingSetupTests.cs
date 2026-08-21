using ChurchBooks.Accounting.Banking;
using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.Setup;
using ChurchBooks.Core.Finance;
using Xunit;

namespace ChurchBooks.Accounting.Tests;

public sealed class Phase6BankingSetupTests
{
    [Fact]
    public void TerminologyCatalog_UsesDefaults()
    {
        var catalog = new TerminologyCatalog();

        Assert.Equal("Members", catalog.Plural(TerminologyKeys.Member));
    }

    [Fact]
    public void TerminologyCatalog_OverridesWithoutChangingKey()
    {
        var catalog = new TerminologyCatalog(
            new[] { new TerminologyDefinition(TerminologyKeys.Member, "Partner", "Partners") });

        Assert.Equal("Partner", catalog.Singular(TerminologyKeys.Member));
        Assert.Equal("Offering", catalog.Singular(TerminologyKeys.Offering));
    }

    [Fact]
    public void OrganizationProfile_RejectsInvalidFiscalMonth()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OrganizationProfile("Church", "Church", CurrencyCode.Php, 13, "PH"));
    }

    [Fact]
    public void BankAccount_RejectsInvalidLastFour()
    {
        Assert.Throws<ArgumentException>(() =>
            new BankAccount(
                Guid.NewGuid(),
                "Bank",
                "Institution",
                "12A4",
                CurrencyCode.Php,
                Guid.NewGuid()));
    }

    [Fact]
    public void BankDeposit_RequiresContribution()
    {
        Assert.Throws<ArgumentException>(() =>
            new BankDeposit(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new DateOnly(2026, 8, 20),
                CurrencyCode.Php,
                Array.Empty<DepositContributionAllocation>(),
                string.Empty,
                string.Empty,
                Guid.NewGuid()));
    }

    [Fact]
    public void BankDeposit_RejectsDuplicateContribution()
    {
        var contributionId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            new BankDeposit(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new DateOnly(2026, 8, 20),
                CurrencyCode.Php,
                new[]
                {
                    new DepositContributionAllocation(contributionId, 10m),
                    new DepositContributionAllocation(contributionId, 10m)
                },
                string.Empty,
                string.Empty,
                Guid.NewGuid()));
    }

    [Fact]
    public void BankDeposit_TotalIsAllocationSum()
    {
        var deposit = new BankDeposit(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 20),
            CurrencyCode.Php,
            new[]
            {
                new DepositContributionAllocation(Guid.NewGuid(), 100m),
                new DepositContributionAllocation(Guid.NewGuid(), 50m)
            },
            string.Empty,
            string.Empty,
            Guid.NewGuid());

        Assert.Equal(150m, deposit.TotalAmount);
    }

    [Fact]
    public void GivingCategoryIncomeMapping_RequiresIds()
    {
        Assert.Throws<ArgumentException>(() =>
            new GivingCategoryIncomeMapping(Guid.Empty, Guid.NewGuid()));
    }
}
