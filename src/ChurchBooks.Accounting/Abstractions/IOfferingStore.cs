using ChurchBooks.Accounting.Offerings;

namespace ChurchBooks.Accounting.Abstractions;

public interface IOfferingStore
{
    Task AddBatchAsync(OfferingBatch batch, CancellationToken cancellationToken = default);
    Task UpdateBatchAsync(OfferingBatch batch, CancellationToken cancellationToken = default);
    Task<OfferingBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OfferingBatch>> GetBatchesAsync(DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken cancellationToken = default);
    Task<bool> ContributionExistsAsync(Guid contributionId, CancellationToken cancellationToken = default);
    Task SaveContributionAsync(Contribution contribution, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Contribution>> GetContributionsAsync(Guid? personId = null, DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken cancellationToken = default);
}
