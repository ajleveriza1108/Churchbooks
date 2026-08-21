using ChurchBooks.Accounting.Giving;
using ChurchBooks.Accounting.Offerings;
using ChurchBooks.Core.Finance;
using Xunit;

namespace ChurchBooks.Accounting.Tests;

public sealed class OfferingAnalyticsTests
{
    private static readonly Guid BatchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PersonId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CategoryId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid FundId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void Contribution_RequiresAtLeastOneBreakdownLine()
    {
        Assert.Throws<ArgumentException>(() => new Contribution(Guid.NewGuid(), BatchId, PersonId, new DateOnly(2026, 8, 16), CurrencyCode.Php, Array.Empty<ContributionLine>()));
    }

    [Fact]
    public void Contribution_RejectsDuplicateCategoryFundRows()
    {
        var lines = new[]
        {
            new ContributionLine(Guid.NewGuid(), CategoryId, FundId, 100m),
            new ContributionLine(Guid.NewGuid(), CategoryId, FundId, 50m)
        };
        Assert.Throws<ArgumentException>(() => new Contribution(Guid.NewGuid(), BatchId, PersonId, new DateOnly(2026, 8, 16), CurrencyCode.Php, lines));
    }

    [Fact]
    public void PeriodTotals_CalculateWeekMonthYearAndAllTime()
    {
        var asOf = new DateOnly(2026, 8, 20);
        var rows = new[]
        {
            Make(new DateOnly(2026, 8, 18), 100m),
            Make(new DateOnly(2026, 8, 2), 200m),
            Make(new DateOnly(2026, 3, 1), 300m),
            Make(new DateOnly(2025, 12, 31), 400m)
        };
        var totals = OfferingAnalyticsCalculator.CalculateTotals(rows, asOf);
        Assert.Equal(100m, totals.Week);
        Assert.Equal(300m, totals.Month);
        Assert.Equal(600m, totals.Year);
        Assert.Equal(1000m, totals.AllTime);
    }

    [Fact]
    public void WeeklyPeriod_StartsOnMonday()
    {
        Assert.Equal(new DateOnly(2026, 8, 17), OfferingAnalyticsCalculator.StartOfWeek(new DateOnly(2026, 8, 20)));
        Assert.Equal(new DateOnly(2026, 8, 17), OfferingAnalyticsCalculator.StartOfWeek(new DateOnly(2026, 8, 23)));
    }

    [Fact]
    public void MonthlyTrend_ReturnsTwelveOrderedPoints()
    {
        var points = OfferingAnalyticsCalculator.BuildTrend(new[] { Make(new DateOnly(2026, 8, 2), 250m) }, OfferingPeriodGranularity.Month, new DateOnly(2026, 8, 20));
        Assert.Equal(12, points.Count);
        Assert.Equal("Sep 2025", points[0].Label);
        Assert.Equal("Aug 2026", points[^1].Label);
        Assert.Equal(250m, points[^1].Amount);
    }

    [Fact]
    public void CategoryBreakdown_SumsLinesByGivingCategory()
    {
        var category2 = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var rows = new[]
        {
            Make(new DateOnly(2026, 8, 18), 100m),
            new Contribution(Guid.NewGuid(), BatchId, PersonId, new DateOnly(2026, 8, 19), CurrencyCode.Php,
                new[] { new ContributionLine(Guid.NewGuid(), category2, FundId, 40m) })
        };
        var categories = new Dictionary<Guid, GivingCategory>
        {
            [CategoryId] = new(CategoryId, "TITHE", "Tithes"),
            [category2] = new(category2, "MISSIONS", "Missions")
        };
        var points = OfferingAnalyticsCalculator.BuildCategoryBreakdown(rows, categories, OfferingPeriodGranularity.Month, new DateOnly(2026, 8, 20));
        Assert.Equal(2, points.Count);
        Assert.Equal("Tithes", points[0].Label);
        Assert.Equal(100m, points[0].Amount);
    }

    [Fact]
    public void OfferingBatch_ClosePreservesIdentityAndRequiresTimestamp()
    {
        var batch = new OfferingBatch(Guid.NewGuid(), new DateOnly(2026, 8, 16), "Sunday Service");
        var closed = batch.Close(new DateTimeOffset(2026, 8, 16, 12, 0, 0, TimeSpan.FromHours(8)));
        Assert.Equal(batch.Id, closed.Id);
        Assert.Equal(OfferingBatchStatus.Closed, closed.Status);
        Assert.NotNull(closed.ClosedUtc);
        Assert.Throws<ArgumentException>(() => new OfferingBatch(Guid.NewGuid(), new DateOnly(2026, 8, 16), "Bad", status: OfferingBatchStatus.Closed));
    }

    private static Contribution Make(DateOnly date, decimal amount) => new(
        Guid.NewGuid(), BatchId, PersonId, date, CurrencyCode.Php,
        new[] { new ContributionLine(Guid.NewGuid(), CategoryId, FundId, amount) });
}
