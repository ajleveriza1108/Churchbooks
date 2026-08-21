using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.Funds;
using ChurchBooks.Accounting.Giving;
using ChurchBooks.Accounting.Offerings;
using ChurchBooks.Accounting.People;

namespace ChurchBooks.Accounting.Services;

public sealed class OfferingManagementService
{
    private readonly IOfferingStore _offeringStore;
    private readonly IPeopleGivingStore _peopleStore;
    private readonly IFundAccountingStore _fundStore;

    public OfferingManagementService(IOfferingStore offeringStore, IPeopleGivingStore peopleStore, IFundAccountingStore fundStore)
    {
        _offeringStore = offeringStore ?? throw new ArgumentNullException(nameof(offeringStore));
        _peopleStore = peopleStore ?? throw new ArgumentNullException(nameof(peopleStore));
        _fundStore = fundStore ?? throw new ArgumentNullException(nameof(fundStore));
    }

    public async Task<OfferingBatch> CreateBatchAsync(OfferingBatch batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Status != OfferingBatchStatus.Open) throw new OfferingManagementException("A new offering batch must start open.");
        await _offeringStore.AddBatchAsync(batch, cancellationToken);
        return batch;
    }

    public async Task<OfferingBatch> CloseBatchAsync(Guid batchId, DateTimeOffset? closedUtc = null, CancellationToken cancellationToken = default)
    {
        var batch = await _offeringStore.GetBatchAsync(batchId, cancellationToken)
            ?? throw new OfferingManagementException("The selected offering batch no longer exists.");
        var closed = batch.Close(closedUtc ?? DateTimeOffset.UtcNow);
        await _offeringStore.UpdateBatchAsync(closed, cancellationToken);
        return closed;
    }

    public async Task<Contribution> RecordContributionAsync(Contribution contribution, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contribution);
        if (await _offeringStore.ContributionExistsAsync(contribution.Id, cancellationToken))
            throw new OfferingManagementException("This contribution has already been recorded.");

        var batch = await _offeringStore.GetBatchAsync(contribution.BatchId, cancellationToken)
            ?? throw new OfferingManagementException("The selected offering batch does not exist.");
        if (batch.Status != OfferingBatchStatus.Open)
            throw new OfferingManagementException("The selected offering batch is closed and cannot accept new contributions.");
        if (contribution.ReceivedDate != batch.ServiceDate)
            throw new OfferingManagementException("Contribution date must match the service/offering batch date.");

        var person = await _peopleStore.GetPersonAsync(contribution.PersonId, cancellationToken)
            ?? throw new OfferingManagementException("The selected person does not exist.");
        if (person.Status != PersonStatus.Active)
            throw new OfferingManagementException("Archived people cannot receive new contribution entries.");
        if (!person.IsDonor)
            throw new OfferingManagementException("Mark the person as a donor before recording an offering for that individual.");

        var categories = await _peopleStore.GetGivingCategoriesAsync(includeArchived: true, cancellationToken);
        var categoryMap = categories.ToDictionary(static category => category.Id);
        foreach (var line in contribution.Lines)
        {
            if (!categoryMap.TryGetValue(line.GivingCategoryId, out var category) || category.Status != GivingCategoryStatus.Active)
                throw new OfferingManagementException("Every contribution line must use an active giving category.");
        }

        var fundIds = contribution.Lines.Select(static line => line.FundId).Distinct().ToArray();
        var funds = await _fundStore.GetFundsAsync(fundIds, cancellationToken);
        foreach (var fundId in fundIds)
        {
            if (!funds.TryGetValue(fundId, out var fund) || fund.Status != FundStatus.Active)
                throw new OfferingManagementException("Every contribution line must use an active fund.");
        }

        await _offeringStore.SaveContributionAsync(contribution, cancellationToken);
        return contribution;
    }
}
