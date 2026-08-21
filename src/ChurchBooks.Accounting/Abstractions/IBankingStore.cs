using ChurchBooks.Accounting.Banking;
using ChurchBooks.Accounting.ChartOfAccounts;

namespace ChurchBooks.Accounting.Abstractions;

public interface IBankingStore
{
    Task AddBankAccountAsync(BankAccount bankAccount, Account ledgerAccount, CancellationToken cancellationToken = default);
    Task UpdateBankAccountAsync(BankAccount bankAccount, CancellationToken cancellationToken = default);
    Task<BankAccount?> GetBankAccountAsync(Guid bankAccountId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BankAccount>> GetBankAccountsAsync(bool includeArchived = false, CancellationToken cancellationToken = default);
    Task SetGivingCategoryIncomeMappingAsync(GivingCategoryIncomeMapping mapping, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, Guid>> GetGivingCategoryIncomeMappingsAsync(CancellationToken cancellationToken = default);
    Task SaveDepositAsync(BankDeposit deposit, CancellationToken cancellationToken = default);
    Task<BankDeposit?> GetDepositAsync(Guid depositId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BankDeposit>> GetDepositsAsync(CancellationToken cancellationToken = default);
    Task<bool> IsContributionAlreadyDepositedAsync(Guid contributionId, CancellationToken cancellationToken = default);
    Task MarkDepositPostedAsync(Guid depositId, Guid journalEntryId, DateTimeOffset postedUtc, CancellationToken cancellationToken = default);
}
