using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.Journals;
using ChurchBooks.Accounting.Periods;
using ChurchBooks.Core.Finance;
using Xunit;

namespace ChurchBooks.Accounting.Tests;

public sealed class JournalEntryValidatorTests
{
    [Fact]
    public void ValidateForPosting_BalancedJournal_Passes()
    {
        var period = OpenAugust();
        var cash = ActiveAccount("1000", "Cash", AccountType.Asset);
        var giving = ActiveAccount("4000", "Contribution Income", AccountType.Income);
        var entry = Entry(period.Id, cash.Id, giving.Id, 1000m, 1000m);
        var accounts = new Dictionary<Guid, Account> { [cash.Id] = cash, [giving.Id] = giving };

        JournalEntryValidator.ValidateForPosting(entry, period, accounts);
    }

    [Fact]
    public void ValidateForPosting_UnbalancedJournal_IsRejected()
    {
        var period = OpenAugust();
        var cash = ActiveAccount("1000", "Cash", AccountType.Asset);
        var giving = ActiveAccount("4000", "Contribution Income", AccountType.Income);
        var entry = Entry(period.Id, cash.Id, giving.Id, 1000m, 999m);
        var accounts = new Dictionary<Guid, Account> { [cash.Id] = cash, [giving.Id] = giving };

        var error = Assert.Throws<JournalPostingException>(() => JournalEntryValidator.ValidateForPosting(entry, period, accounts));
        Assert.Contains("Total debits must equal total credits exactly in the base currency.", error.Errors);
    }

    [Fact]
    public void ValidateForPosting_ClosedPeriod_IsRejected()
    {
        var period = new AccountingPeriod(Guid.NewGuid(), "August 2026", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), AccountingPeriodStatus.Closed);
        var cash = ActiveAccount("1000", "Cash", AccountType.Asset);
        var giving = ActiveAccount("4000", "Contribution Income", AccountType.Income);
        var entry = Entry(period.Id, cash.Id, giving.Id, 1000m, 1000m);
        var accounts = new Dictionary<Guid, Account> { [cash.Id] = cash, [giving.Id] = giving };

        var error = Assert.Throws<JournalPostingException>(() => JournalEntryValidator.ValidateForPosting(entry, period, accounts));
        Assert.Contains("The accounting period is closed.", error.Errors);
    }

    [Fact]
    public void ValidateForPosting_InactiveAccount_IsRejected()
    {
        var period = OpenAugust();
        var cash = new Account(Guid.NewGuid(), "1000", "Cash", AccountType.Asset, AccountStatus.Inactive);
        var giving = ActiveAccount("4000", "Contribution Income", AccountType.Income);
        var entry = Entry(period.Id, cash.Id, giving.Id, 1000m, 1000m);
        var accounts = new Dictionary<Guid, Account> { [cash.Id] = cash, [giving.Id] = giving };

        var error = Assert.Throws<JournalPostingException>(() => JournalEntryValidator.ValidateForPosting(entry, period, accounts));
        Assert.Contains("Account 1000 is inactive and cannot receive new postings.", error.Errors);
    }

    private static AccountingPeriod OpenAugust() => new(
        Guid.NewGuid(),
        "August 2026",
        new DateOnly(2026, 8, 1),
        new DateOnly(2026, 8, 31));

    private static Account ActiveAccount(string code, string name, AccountType type) => new(Guid.NewGuid(), code, name, type);

    private static JournalEntry Entry(Guid periodId, Guid debitAccountId, Guid creditAccountId, decimal debit, decimal credit) => new(
        Guid.NewGuid(),
        "JE-000001",
        periodId,
        new DateOnly(2026, 8, 16),
        "Sunday offering",
        CurrencyCode.Php,
        new[]
        {
            new JournalLine(Guid.NewGuid(), debitAccountId, debit, 0m, "Cash received"),
            new JournalLine(Guid.NewGuid(), creditAccountId, 0m, credit, "Contribution income")
        });
}
