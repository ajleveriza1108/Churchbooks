using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.Funds;
using ChurchBooks.Accounting.Journals;
using ChurchBooks.Core.Finance;
using FsCheck.Xunit;

namespace ChurchBooks.Accounting.Tests;

public sealed class FundInvariantPropertyTests
{
    [Property(MaxTest = 250, QuietOnSuccess = true)]
    public bool SplitDonation_RemainsExactlyBalancedWithinEveryFund(uint firstRaw, uint secondRaw)
    {
        var firstAmount = ToPositiveAmount(firstRaw);
        var secondAmount = ToPositiveAmount(secondRaw);
        var cash = new Account(Guid.NewGuid(), "1000", "Cash", AccountType.Asset);
        var income = new Account(Guid.NewGuid(), "4000", "Giving", AccountType.Income);
        var firstFund = new Fund(Guid.NewGuid(), "F1", "Fund One");
        var secondFund = new Fund(Guid.NewGuid(), "F2", "Fund Two");
        var lines = new[]
        {
            new JournalLine(Guid.NewGuid(), cash.Id, firstAmount, 0m),
            new JournalLine(Guid.NewGuid(), income.Id, 0m, firstAmount),
            new JournalLine(Guid.NewGuid(), cash.Id, secondAmount, 0m),
            new JournalLine(Guid.NewGuid(), income.Id, 0m, secondAmount)
        };
        var journal = new JournalEntry(Guid.NewGuid(), "PROP-FUND", Guid.NewGuid(), new DateOnly(2026, 8, 19), "Property fund split", CurrencyCode.Php, lines);
        var fundJournal = new FundJournalEntry(journal, new[]
        {
            new FundAssignment(lines[0].Id, firstFund.Id),
            new FundAssignment(lines[1].Id, firstFund.Id),
            new FundAssignment(lines[2].Id, secondFund.Id),
            new FundAssignment(lines[3].Id, secondFund.Id)
        });

        return fundJournal.FundIds.All(fundId =>
        {
            var fundLines = journal.Lines.Where(line => fundJournal.GetFundId(line.Id) == fundId);
            return fundLines.Sum(line => line.Debit) == fundLines.Sum(line => line.Credit);
        });
    }

    [Property(MaxTest = 250, QuietOnSuccess = true)]
    public bool InternalFundTransfer_HasEqualAndOppositeFundBalanceImpacts(uint rawCents)
    {
        var amount = ToPositiveAmount(rawCents);
        var source = new Fund(Guid.NewGuid(), "SRC", "Source");
        var destination = new Fund(Guid.NewGuid(), "DST", "Destination");
        var bank = new Account(Guid.NewGuid(), "1000", "Checking", AccountType.Asset);
        var clearing = new Account(Guid.NewGuid(), "3100", "Interfund Transfer Clearing", AccountType.Equity);
        var transfer = FundTransferJournalFactory.Create(
            Guid.NewGuid(), "PROP-FT", Guid.NewGuid(), new DateOnly(2026, 8, 19), "Property transfer", CurrencyCode.Php,
            source, destination, bank, bank, clearing, amount);
        var accounts = new Dictionary<Guid, Account> { [bank.Id] = bank, [clearing.Id] = clearing };

        var sourceImpact = FundBalanceCalculator.CalculateEntryImpact(transfer, source.Id, accounts);
        var destinationImpact = FundBalanceCalculator.CalculateEntryImpact(transfer, destination.Id, accounts);

        return sourceImpact == -amount && destinationImpact == amount && sourceImpact + destinationImpact == 0m;
    }

    private static decimal ToPositiveAmount(uint rawCents) => ((rawCents % 100_000_000u) + 1u) / 100m;
}
