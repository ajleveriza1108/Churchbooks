using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.GeneralLedger;

namespace ChurchBooks.Accounting.Reporting;

public sealed record TrialBalanceRow(
    Guid AccountId,
    string Code,
    string Name,
    AccountType Type,
    decimal Debit,
    decimal Credit);

public sealed record TrialBalanceReport(
    DateOnly AsOfDate,
    IReadOnlyList<TrialBalanceRow> Rows)
{
    public decimal TotalDebit => Rows.Sum(static row => row.Debit);
    public decimal TotalCredit => Rows.Sum(static row => row.Credit);
    public decimal Difference => TotalDebit - TotalCredit;
    public bool IsBalanced => Difference == 0m;
}

public sealed record IncomeExpenseRow(
    Guid AccountId,
    string Code,
    string Name,
    AccountType Type,
    decimal Amount);

public sealed record IncomeExpenseReport(
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<IncomeExpenseRow> IncomeRows,
    IReadOnlyList<IncomeExpenseRow> ExpenseRows)
{
    public decimal TotalIncome => IncomeRows.Sum(static row => row.Amount);
    public decimal TotalExpenses => ExpenseRows.Sum(static row => row.Amount);
    public decimal NetIncome => TotalIncome - TotalExpenses;
}

public sealed record GeneralLedgerReportRow(
    Guid JournalEntryId,
    string EntryNumber,
    DateOnly PostingDate,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance,
    string Description,
    string Reference,
    string Memo);

public sealed record GeneralLedgerReport(
    Account Account,
    DateOnly FromDate,
    DateOnly ToDate,
    decimal OpeningBalance,
    IReadOnlyList<GeneralLedgerReportRow> Rows)
{
    public decimal TotalDebit => Rows.Sum(static row => row.Debit);
    public decimal TotalCredit => Rows.Sum(static row => row.Credit);
    public decimal ClosingBalance => Rows.Count == 0 ? OpeningBalance : Rows[^1].RunningBalance;
}

public sealed class AccountantReportingService
{
    private readonly IAccountingStore _store;

    public AccountantReportingService(IAccountingStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<TrialBalanceReport> BuildTrialBalanceAsync(
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        var accounts = await GetAllAccountsAsync(cancellationToken);
        var rows = new List<TrialBalanceRow>(accounts.Count);

        foreach (var account in accounts)
        {
            var ledger = await _store.GetLedgerAsync(account.Id, toDate: asOfDate, cancellationToken: cancellationToken);
            var calculated = LedgerCalculator.Calculate(account, ledger);
            var debit = 0m;
            var credit = 0m;

            if (account.NormalBalance == AccountNormalBalance.Debit)
            {
                if (calculated.Balance >= 0m) debit = calculated.Balance;
                else credit = -calculated.Balance;
            }
            else
            {
                if (calculated.Balance >= 0m) credit = calculated.Balance;
                else debit = -calculated.Balance;
            }

            if (debit != 0m || credit != 0m)
            {
                rows.Add(new TrialBalanceRow(account.Id, account.Code, account.Name, account.Type, debit, credit));
            }
        }

        return new TrialBalanceReport(asOfDate, rows
            .OrderBy(static row => row.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }

    public async Task<IncomeExpenseReport> BuildIncomeExpenseAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        EnsureDateRange(fromDate, toDate);

        var incomeAccounts = await _store.GetAccountsByTypeAsync(AccountType.Income, cancellationToken);
        var expenseAccounts = await _store.GetAccountsByTypeAsync(AccountType.Expense, cancellationToken);

        var incomeRows = await BuildIncomeExpenseRowsAsync(incomeAccounts, fromDate, toDate, cancellationToken);
        var expenseRows = await BuildIncomeExpenseRowsAsync(expenseAccounts, fromDate, toDate, cancellationToken);

        return new IncomeExpenseReport(fromDate, toDate, incomeRows, expenseRows);
    }

    public async Task<GeneralLedgerReport> BuildGeneralLedgerAsync(
        Guid accountId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("A valid account is required.", nameof(accountId));
        }

        EnsureDateRange(fromDate, toDate);

        var accounts = await _store.GetAccountsAsync(new[] { accountId }, cancellationToken);
        if (!accounts.TryGetValue(accountId, out var account))
        {
            throw new InvalidOperationException("The selected ledger account no longer exists.");
        }

        var openingBalance = 0m;
        if (fromDate > DateOnly.MinValue)
        {
            var openingLines = await _store.GetLedgerAsync(
                accountId,
                toDate: fromDate.AddDays(-1),
                cancellationToken: cancellationToken);
            openingBalance = LedgerCalculator.Calculate(account, openingLines).Balance;
        }

        var lines = await _store.GetLedgerAsync(accountId, fromDate, toDate, cancellationToken);
        var runningBalance = openingBalance;
        var rows = new List<GeneralLedgerReportRow>(lines.Count);

        foreach (var line in lines.OrderBy(static line => line.PostingDate).ThenBy(static line => line.EntryNumber, StringComparer.OrdinalIgnoreCase))
        {
            runningBalance += account.NormalBalance == AccountNormalBalance.Debit
                ? line.Debit - line.Credit
                : line.Credit - line.Debit;

            rows.Add(new GeneralLedgerReportRow(
                line.JournalEntryId,
                line.EntryNumber,
                line.PostingDate,
                line.Debit,
                line.Credit,
                runningBalance,
                line.Description,
                line.Reference,
                line.Memo));
        }

        return new GeneralLedgerReport(account, fromDate, toDate, openingBalance, rows);
    }

    private async Task<IReadOnlyList<IncomeExpenseRow>> BuildIncomeExpenseRowsAsync(
        IReadOnlyList<Account> accounts,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        var rows = new List<IncomeExpenseRow>(accounts.Count);
        foreach (var account in accounts)
        {
            var ledger = await _store.GetLedgerAsync(account.Id, fromDate, toDate, cancellationToken);
            var amount = LedgerCalculator.Calculate(account, ledger).Balance;
            if (amount != 0m)
            {
                rows.Add(new IncomeExpenseRow(account.Id, account.Code, account.Name, account.Type, amount));
            }
        }

        return rows
            .OrderBy(static row => row.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<IReadOnlyList<Account>> GetAllAccountsAsync(CancellationToken cancellationToken)
    {
        var accounts = new List<Account>();
        foreach (var type in Enum.GetValues<AccountType>())
        {
            accounts.AddRange(await _store.GetAccountsByTypeAsync(type, cancellationToken));
        }

        return accounts
            .GroupBy(static account => account.Id)
            .Select(static group => group.First())
            .OrderBy(static account => account.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static account => account.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void EnsureDateRange(DateOnly fromDate, DateOnly toDate)
    {
        if (fromDate > toDate)
        {
            throw new ArgumentException("The report start date cannot be after the end date.");
        }
    }
}
