using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.Periods;

namespace ChurchBooks.Accounting.Journals;

public static class JournalEntryValidator
{
    public static void ValidateForPosting(
        JournalEntry entry,
        AccountingPeriod period,
        IReadOnlyDictionary<Guid, Account> accounts)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(period);
        ArgumentNullException.ThrowIfNull(accounts);

        var errors = new List<string>();

        if (entry.PeriodId != period.Id)
        {
            errors.Add("The journal entry does not belong to the supplied accounting period.");
        }

        if (period.Status != AccountingPeriodStatus.Open)
        {
            errors.Add("The accounting period is closed.");
        }

        if (!period.Contains(entry.PostingDate))
        {
            errors.Add("The posting date is outside the accounting period.");
        }

        if (entry.Lines.Count < 2)
        {
            errors.Add("A journal entry requires at least two lines.");
        }

        if (entry.Lines.Select(static line => line.Id).Distinct().Count() != entry.Lines.Count)
        {
            errors.Add("Journal line IDs must be unique within an entry.");
        }

        if (entry.TotalDebit <= 0m || entry.TotalCredit <= 0m)
        {
            errors.Add("A journal entry must contain positive debit and credit totals.");
        }

        if (entry.TotalDebit != entry.TotalCredit)
        {
            errors.Add("Total debits must equal total credits exactly in the base currency.");
        }

        foreach (var line in entry.Lines)
        {
            if (!accounts.TryGetValue(line.AccountId, out var account))
            {
                errors.Add($"Journal line {line.Id:D} references an unknown account.");
                continue;
            }

            if (account.Status != AccountStatus.Active)
            {
                errors.Add($"Account {account.Code} is inactive and cannot receive new postings.");
            }

            if (!account.AllowDirectPosting)
            {
                errors.Add($"Account {account.Code} is a control/header account and does not allow direct posting.");
            }
        }

        if (errors.Count > 0)
        {
            throw new JournalPostingException(errors);
        }
    }
}
