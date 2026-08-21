using ChurchBooks.Accounting.ChartOfAccounts;

namespace ChurchBooks.Accounting.Funds;

public static class FundJournalValidator
{
    public static FundPostingAssessment AssessForPosting(
        FundJournalEntry entry,
        IReadOnlyDictionary<Guid, Fund> funds,
        IReadOnlyDictionary<Guid, Account> accounts,
        IReadOnlyDictionary<Guid, decimal> currentBalances)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(funds);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(currentBalances);

        var errors = new List<string>();
        var warnings = new List<string>();

        foreach (var fundId in entry.FundIds)
        {
            if (!funds.TryGetValue(fundId, out var fund))
            {
                errors.Add($"Journal entry references unknown fund {fundId:D}.");
                continue;
            }

            if (fund.Status != FundStatus.Active)
            {
                errors.Add($"Fund {fund.Code} is archived and cannot receive new postings.");
            }

            var fundLines = entry.Entry.Lines.Where(line => entry.GetFundId(line.Id) == fundId).ToArray();
            var totalDebit = fundLines.Sum(static line => line.Debit);
            var totalCredit = fundLines.Sum(static line => line.Credit);
            if (totalDebit <= 0m || totalCredit <= 0m || totalDebit != totalCredit)
            {
                errors.Add($"Fund {fund.Code} must balance exactly within the journal entry. Debit={totalDebit} Credit={totalCredit}.");
            }

            if (!currentBalances.TryGetValue(fundId, out var currentBalance))
            {
                currentBalance = 0m;
            }

            var projectedBalance = currentBalance + FundBalanceCalculator.CalculateEntryImpact(entry, fundId, accounts);
            if (projectedBalance < 0m)
            {
                var message = $"Fund {fund.Code} would have a negative balance of {projectedBalance}.";
                if (fund.OverspendPolicy == FundOverspendPolicy.Block)
                {
                    errors.Add(message + " Posting is blocked by the fund overspend policy.");
                }
                else if (fund.OverspendPolicy == FundOverspendPolicy.Warn)
                {
                    warnings.Add(message + " Review before posting.");
                }
            }
        }

        return new FundPostingAssessment(errors, warnings);
    }
}
