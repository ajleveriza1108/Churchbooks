using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.Journals;

namespace ChurchBooks.Accounting.Funds;

public static class FundBalanceCalculator
{
    public static decimal Calculate(IEnumerable<FundActivityLine> activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        return activity.Sum(static line => line.NetAssetImpact);
    }

    public static decimal CalculateEntryImpact(
        FundJournalEntry entry,
        Guid fundId,
        IReadOnlyDictionary<Guid, Account> accounts)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(accounts);

        decimal impact = 0m;
        foreach (JournalLine line in entry.Entry.Lines)
        {
            if (entry.GetFundId(line.Id) != fundId)
            {
                continue;
            }

            if (!accounts.TryGetValue(line.AccountId, out var account))
            {
                throw new InvalidOperationException($"Account {line.AccountId:D} is required to calculate fund balance impact.");
            }

            if (account.Type is AccountType.Asset or AccountType.Liability)
            {
                impact += line.Debit - line.Credit;
            }
        }

        return impact;
    }
}
