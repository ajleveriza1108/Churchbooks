using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.Offerings;

namespace ChurchBooks.Accounting.Services;

public sealed class OfferingReportingService
{
    private readonly IOfferingStore _offeringStore;
    private readonly IPeopleGivingStore _peopleStore;

    public OfferingReportingService(IOfferingStore offeringStore, IPeopleGivingStore peopleStore)
    {
        _offeringStore = offeringStore ?? throw new ArgumentNullException(nameof(offeringStore));
        _peopleStore = peopleStore ?? throw new ArgumentNullException(nameof(peopleStore));
    }

    public async Task<OfferingPeriodTotals> GetTotalsAsync(Guid? personId, DateOnly asOfDate, CancellationToken cancellationToken = default)
    {
        var contributions = await _offeringStore.GetContributionsAsync(personId, toDate: asOfDate, cancellationToken: cancellationToken);
        return OfferingAnalyticsCalculator.CalculateTotals(contributions, asOfDate);
    }

    public async Task<IReadOnlyList<OfferingChartPoint>> GetTrendAsync(Guid? personId, OfferingPeriodGranularity granularity, DateOnly asOfDate, CancellationToken cancellationToken = default)
    {
        var contributions = await _offeringStore.GetContributionsAsync(personId, toDate: asOfDate, cancellationToken: cancellationToken);
        return OfferingAnalyticsCalculator.BuildTrend(contributions, granularity, asOfDate);
    }

    public async Task<IReadOnlyList<OfferingChartPoint>> GetCategoryBreakdownAsync(Guid? personId, OfferingPeriodGranularity granularity, DateOnly asOfDate, CancellationToken cancellationToken = default)
    {
        var contributions = await _offeringStore.GetContributionsAsync(personId, toDate: asOfDate, cancellationToken: cancellationToken);
        var categories = await _peopleStore.GetGivingCategoriesAsync(includeArchived: true, cancellationToken);
        return OfferingAnalyticsCalculator.BuildCategoryBreakdown(contributions, categories.ToDictionary(static category => category.Id), granularity, asOfDate);
    }
}
