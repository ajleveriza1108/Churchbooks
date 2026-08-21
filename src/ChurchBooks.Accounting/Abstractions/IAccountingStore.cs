using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.GeneralLedger;
using ChurchBooks.Accounting.Journals;
using ChurchBooks.Accounting.Periods;

namespace ChurchBooks.Accounting.Abstractions;

public interface IAccountingStore
{
    Task AddAccountAsync(Account account, CancellationToken cancellationToken = default);
    Task AddPeriodAsync(AccountingPeriod period, CancellationToken cancellationToken = default);
    Task<AccountingPeriod?> GetPeriodAsync(Guid periodId, CancellationToken cancellationToken = default);
    Task<AccountingPeriod?> GetOpenPeriodForDateAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<Account?> GetAccountByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Account>> GetAccountsByTypeAsync(AccountType accountType, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, Account>> GetAccountsAsync(IEnumerable<Guid> accountIds, CancellationToken cancellationToken = default);
    Task<bool> JournalExistsAsync(Guid journalEntryId, CancellationToken cancellationToken = default);
    Task SavePostedJournalAsync(PostedJournalEntry postedEntry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LedgerLine>> GetLedgerAsync(Guid accountId, DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken cancellationToken = default);
}
