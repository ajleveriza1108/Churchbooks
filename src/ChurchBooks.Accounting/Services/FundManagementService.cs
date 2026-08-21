using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.Funds;

namespace ChurchBooks.Accounting.Services;

public sealed class FundManagementService
{
    private readonly IFundAccountingStore _store;

    public FundManagementService(IFundAccountingStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public Task<IReadOnlyList<Fund>> GetFundsAsync(bool includeArchived = false, CancellationToken cancellationToken = default) =>
        _store.GetAllFundsAsync(includeArchived, cancellationToken);

    public async Task<Fund> CreateFundAsync(Fund fund, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fund);
        await EnsureUniqueAsync(fund, currentFundId: null, cancellationToken);
        await _store.AddFundAsync(fund, cancellationToken);
        return fund;
    }

    public async Task<Fund> UpdateFundAsync(Fund fund, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fund);
        await EnsureUniqueAsync(fund, fund.Id, cancellationToken);
        await _store.UpdateFundAsync(fund, cancellationToken);
        return fund;
    }

    public async Task<Fund> ArchiveFundAsync(Guid fundId, DateTimeOffset? archivedUtc = null, CancellationToken cancellationToken = default)
    {
        var fund = await RequireFundAsync(fundId, cancellationToken);
        if (fund.Status == FundStatus.Archived)
        {
            return fund;
        }

        var archived = fund.Archive(archivedUtc ?? DateTimeOffset.UtcNow);
        await _store.UpdateFundAsync(archived, cancellationToken);
        return archived;
    }

    public async Task<Fund> ReactivateFundAsync(Guid fundId, CancellationToken cancellationToken = default)
    {
        var fund = await RequireFundAsync(fundId, cancellationToken);
        if (fund.Status == FundStatus.Active)
        {
            return fund;
        }

        var reactivated = fund.Reactivate();
        await EnsureUniqueAsync(reactivated, reactivated.Id, cancellationToken);
        await _store.UpdateFundAsync(reactivated, cancellationToken);
        return reactivated;
    }

    private async Task<Fund> RequireFundAsync(Guid fundId, CancellationToken cancellationToken)
    {
        if (fundId == Guid.Empty)
        {
            throw new FundManagementException("A valid fund is required.");
        }

        return await _store.GetFundAsync(fundId, cancellationToken)
            ?? throw new FundManagementException("The selected fund no longer exists.");
    }

    private async Task EnsureUniqueAsync(Fund candidate, Guid? currentFundId, CancellationToken cancellationToken)
    {
        var allFunds = await _store.GetAllFundsAsync(includeArchived: true, cancellationToken);
        foreach (var existing in allFunds)
        {
            if (currentFundId.HasValue && existing.Id == currentFundId.Value)
            {
                continue;
            }

            if (string.Equals(existing.Code, candidate.Code, StringComparison.OrdinalIgnoreCase))
            {
                throw new FundManagementException($"Fund code '{candidate.Code}' is already in use.");
            }

            if (string.Equals(existing.Name, candidate.Name, StringComparison.OrdinalIgnoreCase))
            {
                throw new FundManagementException($"Fund name '{candidate.Name}' is already in use.");
            }
        }
    }
}
