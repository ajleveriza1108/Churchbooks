using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.GeneralLedger;
using ChurchBooks.Accounting.Journals;
using ChurchBooks.Core.Finance;
using FsCheck.Xunit;

namespace ChurchBooks.Accounting.Tests;

public sealed class AccountingInvariantPropertyTests
{
    [Property(MaxTest = 250, QuietOnSuccess = true)]
    public bool BalancedTwoLineJournal_RemainsExactlyBalanced(uint rawCents)
    {
        var amount = ToPositiveAmount(rawCents);
        var entry = CreateEntry(amount, amount);

        return entry.IsBalanced && entry.TotalDebit == amount && entry.TotalCredit == amount;
    }

    [Property(MaxTest = 250, QuietOnSuccess = true)]
    public bool UnequalDebitAndCredit_NeverReportsBalanced(uint rawCents)
    {
        var debit = ToPositiveAmount(rawCents);
        var credit = debit + 0.01m;
        var entry = CreateEntry(debit, credit);

        return !entry.IsBalanced && entry.TotalDebit != entry.TotalCredit;
    }

    [Property(MaxTest = 250, QuietOnSuccess = true)]
    public bool DebitNormalLedgerBalance_EqualsDebitMinusCredit(uint rawDebitCents, uint rawCreditCents)
    {
        var debit = ToAmount(rawDebitCents);
        var credit = ToAmount(rawCreditCents);
        var account = new Account(Guid.NewGuid(), "1000", "Cash", AccountType.Asset);
        var balance = LedgerCalculator.Calculate(account, new[] { CreateLedgerLine(account.Id, debit, credit) });

        return balance.Balance == debit - credit && balance.TotalDebit == debit && balance.TotalCredit == credit;
    }

    [Property(MaxTest = 250, QuietOnSuccess = true)]
    public bool CreditNormalLedgerBalance_EqualsCreditMinusDebit(uint rawDebitCents, uint rawCreditCents)
    {
        var debit = ToAmount(rawDebitCents);
        var credit = ToAmount(rawCreditCents);
        var account = new Account(Guid.NewGuid(), "4000", "Contribution Income", AccountType.Income);
        var balance = LedgerCalculator.Calculate(account, new[] { CreateLedgerLine(account.Id, debit, credit) });

        return balance.Balance == credit - debit && balance.TotalDebit == debit && balance.TotalCredit == credit;
    }

    private static decimal ToPositiveAmount(uint rawCents) => ((rawCents % 100_000_000u) + 1u) / 100m;
    private static decimal ToAmount(uint rawCents) => (rawCents % 100_000_000u) / 100m;

    private static JournalEntry CreateEntry(decimal debit, decimal credit)
    {
        var periodId = Guid.NewGuid();
        var debitAccountId = Guid.NewGuid();
        var creditAccountId = Guid.NewGuid();
        return new JournalEntry(
            Guid.NewGuid(),
            "PROP-000001",
            periodId,
            new DateOnly(2026, 8, 19),
            "Property-based accounting invariant",
            CurrencyCode.Php,
            new[]
            {
                new JournalLine(Guid.NewGuid(), debitAccountId, debit, 0m),
                new JournalLine(Guid.NewGuid(), creditAccountId, 0m, credit)
            });
    }

    private static LedgerLine CreateLedgerLine(Guid accountId, decimal debit, decimal credit) => new(
        Guid.NewGuid(),
        "PROP-LEDGER",
        new DateOnly(2026, 8, 19),
        accountId,
        debit,
        credit,
        "Property-based ledger invariant",
        string.Empty,
        string.Empty);
}
