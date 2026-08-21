using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.GeneralLedger;
using ChurchBooks.Accounting.Journals;
using ChurchBooks.Accounting.Periods;
using ChurchBooks.Accounting.Reporting;
using Xunit;

namespace ChurchBooks.Accounting.Tests;

public sealed class Phase10R111AccountantReportingTests
{
    [Fact]
    public async Task TrialBalance_UsesNormalBalancesAndBalancesDebitsAndCredits()
    {
        var cash = AccountOf("1000", "Cash", AccountType.Asset);
        var income = AccountOf("4000", "Giving Income", AccountType.Income);
        var store = new FakeAccountingStore(cash, income);
        store.AddLine(cash, new DateOnly(2026, 8, 1), 500m, 0m);
        store.AddLine(income, new DateOnly(2026, 8, 1), 0m, 500m);

        var report = await new AccountantReportingService(store).BuildTrialBalanceAsync(new DateOnly(2026, 8, 31));

        Assert.Equal(500m, report.TotalDebit);
        Assert.Equal(500m, report.TotalCredit);
        Assert.Equal(0m, report.Difference);
        Assert.True(report.IsBalanced);
    }

    [Fact]
    public async Task TrialBalance_FlipsAbnormalCreditBalanceToDebitColumn()
    {
        var payable = AccountOf("2000", "Accounts Payable", AccountType.Liability);
        var store = new FakeAccountingStore(payable);
        store.AddLine(payable, new DateOnly(2026, 8, 5), 125m, 0m);

        var report = await new AccountantReportingService(store).BuildTrialBalanceAsync(new DateOnly(2026, 8, 31));
        var row = Assert.Single(report.Rows);

        Assert.Equal(125m, row.Debit);
        Assert.Equal(0m, row.Credit);
    }

    [Fact]
    public async Task TrialBalance_AsOfDateExcludesFutureLedgerActivity()
    {
        var cash = AccountOf("1000", "Cash", AccountType.Asset);
        var store = new FakeAccountingStore(cash);
        store.AddLine(cash, new DateOnly(2026, 8, 10), 100m, 0m);
        store.AddLine(cash, new DateOnly(2026, 9, 1), 900m, 0m);

        var report = await new AccountantReportingService(store).BuildTrialBalanceAsync(new DateOnly(2026, 8, 31));

        Assert.Equal(100m, Assert.Single(report.Rows).Debit);
    }

    [Fact]
    public async Task IncomeExpenseReport_CalculatesIncomeExpenseAndNetIncome()
    {
        var giving = AccountOf("4000", "Giving Income", AccountType.Income);
        var utilities = AccountOf("6000", "Utilities", AccountType.Expense);
        var store = new FakeAccountingStore(giving, utilities);
        store.AddLine(giving, new DateOnly(2026, 8, 2), 0m, 1000m);
        store.AddLine(utilities, new DateOnly(2026, 8, 3), 250m, 0m);

        var report = await new AccountantReportingService(store).BuildIncomeExpenseAsync(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31));

        Assert.Equal(1000m, report.TotalIncome);
        Assert.Equal(250m, report.TotalExpenses);
        Assert.Equal(750m, report.NetIncome);
    }

    [Fact]
    public async Task IncomeExpenseReport_ExcludesActivityOutsideDateRange()
    {
        var giving = AccountOf("4000", "Giving Income", AccountType.Income);
        var store = new FakeAccountingStore(giving);
        store.AddLine(giving, new DateOnly(2026, 7, 31), 0m, 900m);
        store.AddLine(giving, new DateOnly(2026, 8, 5), 0m, 100m);

        var report = await new AccountantReportingService(store).BuildIncomeExpenseAsync(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31));

        Assert.Equal(100m, report.TotalIncome);
    }

    [Fact]
    public async Task IncomeExpenseReport_RejectsReversedDateRange()
    {
        var service = new AccountantReportingService(new FakeAccountingStore());

        await Assert.ThrowsAsync<ArgumentException>(() => service.BuildIncomeExpenseAsync(
            new DateOnly(2026, 8, 31),
            new DateOnly(2026, 8, 1)));
    }

    [Fact]
    public async Task GeneralLedger_CalculatesOpeningRunningAndClosingBalances()
    {
        var cash = AccountOf("1000", "Cash", AccountType.Asset);
        var store = new FakeAccountingStore(cash);
        store.AddLine(cash, new DateOnly(2026, 7, 31), 100m, 0m, "Opening activity");
        store.AddLine(cash, new DateOnly(2026, 8, 5), 75m, 0m, "Deposit");
        store.AddLine(cash, new DateOnly(2026, 8, 10), 0m, 20m, "Payment");

        var report = await new AccountantReportingService(store).BuildGeneralLedgerAsync(
            cash.Id,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31));

        Assert.Equal(100m, report.OpeningBalance);
        Assert.Equal(175m, report.Rows[0].RunningBalance);
        Assert.Equal(155m, report.Rows[1].RunningBalance);
        Assert.Equal(155m, report.ClosingBalance);
    }

    [Fact]
    public async Task GeneralLedger_RejectsUnknownAccount()
    {
        var service = new AccountantReportingService(new FakeAccountingStore());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.BuildGeneralLedgerAsync(
            Guid.NewGuid(),
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31)));
    }

    private static Account AccountOf(string code, string name, AccountType type) =>
        new(Guid.NewGuid(), code, name, type);

    private sealed class FakeAccountingStore : IAccountingStore
    {
        private readonly List<Account> _accounts;
        private readonly List<LedgerLine> _lines = new();

        public FakeAccountingStore(params Account[] accounts) => _accounts = accounts.ToList();

        public void AddLine(Account account, DateOnly date, decimal debit, decimal credit, string description = "Test")
        {
            _lines.Add(new LedgerLine(
                Guid.NewGuid(),
                "JE-" + (_lines.Count + 1).ToString("000"),
                date,
                account.Id,
                debit,
                credit,
                description,
                string.Empty,
                string.Empty));
        }

        public Task AddAccountAsync(Account account, CancellationToken cancellationToken = default)
        {
            _accounts.Add(account);
            return Task.CompletedTask;
        }

        public Task AddPeriodAsync(AccountingPeriod period, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AccountingPeriod?> GetPeriodAsync(Guid periodId, CancellationToken cancellationToken = default) => Task.FromResult<AccountingPeriod?>(null);
        public Task<AccountingPeriod?> GetOpenPeriodForDateAsync(DateOnly date, CancellationToken cancellationToken = default) => Task.FromResult<AccountingPeriod?>(null);
        public Task<Account?> GetAccountByCodeAsync(string code, CancellationToken cancellationToken = default) => Task.FromResult(_accounts.FirstOrDefault(account => account.Code.Equals(code, StringComparison.OrdinalIgnoreCase)));
        public Task<IReadOnlyList<Account>> GetAccountsByTypeAsync(AccountType accountType, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Account>>(_accounts.Where(account => account.Type == accountType).OrderBy(account => account.Code).ToArray());
        public Task<IReadOnlyDictionary<Guid, Account>> GetAccountsAsync(IEnumerable<Guid> accountIds, CancellationToken cancellationToken = default)
        {
            var ids = accountIds.ToHashSet();
            return Task.FromResult<IReadOnlyDictionary<Guid, Account>>(_accounts.Where(account => ids.Contains(account.Id)).ToDictionary(account => account.Id));
        }
        public Task<bool> JournalExistsAsync(Guid journalEntryId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task SavePostedJournalAsync(PostedJournalEntry postedEntry, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<LedgerLine>> GetLedgerAsync(Guid accountId, DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken cancellationToken = default)
        {
            var result = _lines
                .Where(line => line.AccountId == accountId)
                .Where(line => !fromDate.HasValue || line.PostingDate >= fromDate.Value)
                .Where(line => !toDate.HasValue || line.PostingDate <= toDate.Value)
                .OrderBy(line => line.PostingDate)
                .ThenBy(line => line.EntryNumber)
                .ToArray();
            return Task.FromResult<IReadOnlyList<LedgerLine>>(result);
        }
    }
}
