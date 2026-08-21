using ChurchBooks.Accounting.Funds;

namespace ChurchBooks.Accounting.Abstractions;

public interface IFundAccountingStore
{
    Task AddFundAsync(Fund fund, CancellationToken cancellationToken = default);
    Task UpdateFundAsync(Fund fund, CancellationToken cancellationToken = default);
    Task<Fund?> GetFundAsync(Guid fundId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Fund>> GetAllFundsAsync(bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, Fund>> GetFundsAsync(IEnumerable<Guid> fundIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, decimal>> GetFundBalancesAsync(IEnumerable<Guid> fundIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, decimal>> GetFundBalancesAsOfAsync(IEnumerable<Guid> fundIds, DateOnly asOfDate, CancellationToken cancellationToken = default);
    Task SavePostedFundJournalAsync(PostedFundJournalEntry postedEntry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FundActivityLine>> GetFundActivityAsync(Guid fundId, DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken cancellationToken = default);
}
