using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.Funds;
using ChurchBooks.Core.Finance;
using Xunit;

namespace ChurchBooks.Accounting.Tests;

public sealed class FundTransferJournalFactoryTests
{
    [Fact]
    public void Create_InternalFundTransfer_IsBalancedOverallAndWithinEachFund()
    {
        var source = new Fund(Guid.NewGuid(), "GEN", "General");
        var destination = new Fund(Guid.NewGuid(), "MIS", "Missions", FundRestriction.DonorRestricted);
        var bank = new Account(Guid.NewGuid(), "1000", "Checking", AccountType.Asset);
        var clearing = new Account(Guid.NewGuid(), "3100", "Interfund Transfer Clearing", AccountType.Equity);

        var transfer = FundTransferJournalFactory.Create(
            Guid.NewGuid(), "FT-1", Guid.NewGuid(), new DateOnly(2026, 8, 19), "Fund transfer", CurrencyCode.Php,
            source, destination, bank, bank, clearing, 250m);

        Assert.True(transfer.Entry.IsBalanced);
        Assert.Equal(500m, transfer.Entry.TotalDebit);
        Assert.Equal(500m, transfer.Entry.TotalCredit);
        foreach (var fundId in transfer.FundIds)
        {
            var lines = transfer.Entry.Lines.Where(line => transfer.GetFundId(line.Id) == fundId);
            Assert.Equal(lines.Sum(line => line.Debit), lines.Sum(line => line.Credit));
        }
    }

    [Fact]
    public void Create_InternalFundTransfer_MovesNetAssetsBetweenFundsWithoutChangingConsolidatedBank()
    {
        var source = new Fund(Guid.NewGuid(), "GEN", "General");
        var destination = new Fund(Guid.NewGuid(), "MIS", "Missions");
        var bank = new Account(Guid.NewGuid(), "1000", "Checking", AccountType.Asset);
        var clearing = new Account(Guid.NewGuid(), "3100", "Interfund Transfer Clearing", AccountType.Equity);
        var transfer = FundTransferJournalFactory.Create(
            Guid.NewGuid(), "FT-2", Guid.NewGuid(), new DateOnly(2026, 8, 19), "Fund transfer", CurrencyCode.Php,
            source, destination, bank, bank, clearing, 250m);
        var accounts = new Dictionary<Guid, Account> { [bank.Id] = bank, [clearing.Id] = clearing };

        var sourceImpact = FundBalanceCalculator.CalculateEntryImpact(transfer, source.Id, accounts);
        var destinationImpact = FundBalanceCalculator.CalculateEntryImpact(transfer, destination.Id, accounts);
        var bankDebit = transfer.Entry.Lines.Where(line => line.AccountId == bank.Id).Sum(line => line.Debit);
        var bankCredit = transfer.Entry.Lines.Where(line => line.AccountId == bank.Id).Sum(line => line.Credit);

        Assert.Equal(-250m, sourceImpact);
        Assert.Equal(250m, destinationImpact);
        Assert.Equal(0m, sourceImpact + destinationImpact);
        Assert.Equal(bankDebit, bankCredit);
    }
}
