using ChurchBooks.Accounting.ChartOfAccounts;

namespace ChurchBooks.Accounting.GeneralLedger;

public static class LedgerCalculator
{
    public static LedgerBalance Calculate(Account account, IEnumerable<LedgerLine> lines)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(lines);

        var totalDebit = 0m;
        var totalCredit = 0m;

        foreach (var line in lines)
        {
            if (line.AccountId != account.Id)
            {
                throw new ArgumentException("Ledger contains a line for a different account.", nameof(lines));
            }

            totalDebit += line.Debit;
            totalCredit += line.Credit;
        }

        var balance = account.NormalBalance == AccountNormalBalance.Debit
            ? totalDebit - totalCredit
            : totalCredit - totalDebit;

        return new LedgerBalance(totalDebit, totalCredit, balance, account.NormalBalance);
    }
}
