using ChurchBooks.Accounting.ChartOfAccounts;

namespace ChurchBooks.Accounting.GeneralLedger;

public sealed record LedgerBalance(
    decimal TotalDebit,
    decimal TotalCredit,
    decimal Balance,
    AccountNormalBalance NormalBalance);
