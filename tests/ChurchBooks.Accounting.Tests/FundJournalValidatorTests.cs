using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.Funds;
using ChurchBooks.Accounting.Journals;
using ChurchBooks.Core.Finance;
using Xunit;

namespace ChurchBooks.Accounting.Tests;

public sealed class FundJournalValidatorTests
{
    [Fact]
    public void AssessForPosting_SplitDonationBalancedWithinEachFund_Passes()
    {
        var cash = Account("1000", "Cash", AccountType.Asset);
        var income = Account("4000", "Giving", AccountType.Income);
        var general = new Fund(Guid.NewGuid(), "GEN", "General");
        var missions = new Fund(Guid.NewGuid(), "MIS", "Missions", FundRestriction.DonorRestricted);
        var entry = SplitDonation(cash, income, general, missions, 600m, 400m);

        var result = FundJournalValidator.AssessForPosting(
            entry,
            Funds(general, missions),
            Accounts(cash, income),
            new Dictionary<Guid, decimal> { [general.Id] = 0m, [missions.Id] = 0m });

        Assert.True(result.CanPost);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void AssessForPosting_CrossFundOneSidedEntry_IsRejected()
    {
        var cash = Account("1000", "Cash", AccountType.Asset);
        var income = Account("4000", "Giving", AccountType.Income);
        var general = new Fund(Guid.NewGuid(), "GEN", "General");
        var missions = new Fund(Guid.NewGuid(), "MIS", "Missions");
        var debit = new JournalLine(Guid.NewGuid(), cash.Id, 100m, 0m);
        var credit = new JournalLine(Guid.NewGuid(), income.Id, 0m, 100m);
        var journal = Journal(new[] { debit, credit });
        var entry = new FundJournalEntry(journal, new[]
        {
            new FundAssignment(debit.Id, general.Id),
            new FundAssignment(credit.Id, missions.Id)
        });

        var result = FundJournalValidator.AssessForPosting(
            entry,
            Funds(general, missions),
            Accounts(cash, income),
            new Dictionary<Guid, decimal>());

        Assert.False(result.CanPost);
        Assert.Equal(2, result.Errors.Count(error => error.Contains("must balance exactly within the journal entry", StringComparison.Ordinal)));
    }

    [Fact]
    public void AssessForPosting_ArchivedFund_IsRejected()
    {
        var cash = Account("1000", "Cash", AccountType.Asset);
        var income = Account("4000", "Giving", AccountType.Income);
        var fund = new Fund(Guid.NewGuid(), "OLD", "Old Project").Archive(DateTimeOffset.UtcNow);
        var entry = SingleFundJournal(cash, income, fund, 100m);

        var result = FundJournalValidator.AssessForPosting(
            entry,
            Funds(fund),
            Accounts(cash, income),
            new Dictionary<Guid, decimal> { [fund.Id] = 0m });

        Assert.False(result.CanPost);
        Assert.Contains(result.Errors, error => error.Contains("archived and cannot receive new postings", StringComparison.Ordinal));
    }

    [Fact]
    public void AssessForPosting_BlockPolicyRejectsNegativeProjectedBalance()
    {
        var cash = Account("1000", "Cash", AccountType.Asset);
        var expense = Account("5000", "Mission Expense", AccountType.Expense);
        var fund = new Fund(Guid.NewGuid(), "MIS", "Missions", FundRestriction.DonorRestricted);
        var entry = ExpenseJournal(cash, expense, fund, 20m);

        var result = FundJournalValidator.AssessForPosting(
            entry,
            Funds(fund),
            Accounts(cash, expense),
            new Dictionary<Guid, decimal> { [fund.Id] = 10m });

        Assert.False(result.CanPost);
        Assert.Contains(result.Errors, error => error.Contains("Posting is blocked by the fund overspend policy", StringComparison.Ordinal));
    }

    [Fact]
    public void AssessForPosting_WarnPolicyAllowsPostingButReturnsWarning()
    {
        var cash = Account("1000", "Cash", AccountType.Asset);
        var expense = Account("5000", "Youth Expense", AccountType.Expense);
        var fund = new Fund(Guid.NewGuid(), "YOUTH", "Youth", FundRestriction.BoardDesignated);
        var entry = ExpenseJournal(cash, expense, fund, 20m);

        var result = FundJournalValidator.AssessForPosting(
            entry,
            Funds(fund),
            Accounts(cash, expense),
            new Dictionary<Guid, decimal> { [fund.Id] = 10m });

        Assert.True(result.CanPost);
        Assert.Empty(result.Errors);
        Assert.Single(result.Warnings);
        Assert.Contains("negative balance", result.Warnings[0], StringComparison.Ordinal);
    }

    private static FundJournalEntry SplitDonation(Account cash, Account income, Fund first, Fund second, decimal firstAmount, decimal secondAmount)
    {
        var lines = new[]
        {
            new JournalLine(Guid.NewGuid(), cash.Id, firstAmount, 0m),
            new JournalLine(Guid.NewGuid(), income.Id, 0m, firstAmount),
            new JournalLine(Guid.NewGuid(), cash.Id, secondAmount, 0m),
            new JournalLine(Guid.NewGuid(), income.Id, 0m, secondAmount)
        };
        var journal = Journal(lines);
        return new FundJournalEntry(journal, new[]
        {
            new FundAssignment(lines[0].Id, first.Id),
            new FundAssignment(lines[1].Id, first.Id),
            new FundAssignment(lines[2].Id, second.Id),
            new FundAssignment(lines[3].Id, second.Id)
        });
    }

    private static FundJournalEntry SingleFundJournal(Account cash, Account income, Fund fund, decimal amount)
    {
        var lines = new[]
        {
            new JournalLine(Guid.NewGuid(), cash.Id, amount, 0m),
            new JournalLine(Guid.NewGuid(), income.Id, 0m, amount)
        };
        var journal = Journal(lines);
        return new FundJournalEntry(journal, lines.Select(line => new FundAssignment(line.Id, fund.Id)));
    }

    private static FundJournalEntry ExpenseJournal(Account cash, Account expense, Fund fund, decimal amount)
    {
        var lines = new[]
        {
            new JournalLine(Guid.NewGuid(), expense.Id, amount, 0m),
            new JournalLine(Guid.NewGuid(), cash.Id, 0m, amount)
        };
        var journal = Journal(lines);
        return new FundJournalEntry(journal, lines.Select(line => new FundAssignment(line.Id, fund.Id)));
    }

    private static JournalEntry Journal(IEnumerable<JournalLine> lines) => new(
        Guid.NewGuid(),
        "FUND-VALIDATE",
        Guid.NewGuid(),
        new DateOnly(2026, 8, 19),
        "Fund validation",
        CurrencyCode.Php,
        lines);

    private static Account Account(string code, string name, AccountType type) => new(Guid.NewGuid(), code, name, type);
    private static IReadOnlyDictionary<Guid, Account> Accounts(params Account[] accounts) => accounts.ToDictionary(account => account.Id);
    private static IReadOnlyDictionary<Guid, Fund> Funds(params Fund[] funds) => funds.ToDictionary(fund => fund.Id);
}
