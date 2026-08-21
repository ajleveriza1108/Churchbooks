using ChurchBooks.Accounting.Giving;

namespace ChurchBooks.Accounting.Offerings;

public static class OfferingAnalyticsCalculator
{
    public static OfferingPeriodTotals CalculateTotals(IEnumerable<Contribution> contributions, DateOnly asOfDate)
    {
        ArgumentNullException.ThrowIfNull(contributions);
        var rows = contributions.ToArray();
        var weekStart = StartOfWeek(asOfDate);
        var monthStart = new DateOnly(asOfDate.Year, asOfDate.Month, 1);
        var yearStart = new DateOnly(asOfDate.Year, 1, 1);
        return new OfferingPeriodTotals(
            Sum(rows, weekStart, asOfDate),
            Sum(rows, monthStart, asOfDate),
            Sum(rows, yearStart, asOfDate),
            rows.Where(row => row.ReceivedDate <= asOfDate).Sum(static row => row.TotalAmount));
    }

    public static IReadOnlyList<OfferingChartPoint> BuildTrend(
        IEnumerable<Contribution> contributions,
        OfferingPeriodGranularity granularity,
        DateOnly asOfDate)
    {
        ArgumentNullException.ThrowIfNull(contributions);
        var rows = contributions.Where(row => row.ReceivedDate <= asOfDate).ToArray();
        return granularity switch
        {
            OfferingPeriodGranularity.Week => BuildWeekly(rows, asOfDate, 12),
            OfferingPeriodGranularity.Month => BuildMonthly(rows, asOfDate, 12),
            OfferingPeriodGranularity.Year => BuildYearly(rows, asOfDate, 5),
            _ => Array.Empty<OfferingChartPoint>()
        };
    }

    public static IReadOnlyList<OfferingChartPoint> BuildCategoryBreakdown(
        IEnumerable<Contribution> contributions,
        IReadOnlyDictionary<Guid, GivingCategory> categories,
        OfferingPeriodGranularity granularity,
        DateOnly asOfDate)
    {
        ArgumentNullException.ThrowIfNull(contributions);
        ArgumentNullException.ThrowIfNull(categories);
        var (start, end) = CurrentRange(granularity, asOfDate);
        return contributions
            .Where(row => row.ReceivedDate >= start && row.ReceivedDate <= end)
            .SelectMany(static row => row.Lines)
            .GroupBy(static line => line.GivingCategoryId)
            .Select(group => new OfferingChartPoint(
                categories.TryGetValue(group.Key, out var category) ? category.Name : "Unknown category",
                group.Sum(static line => line.Amount)))
            .OrderByDescending(static point => point.Amount)
            .ThenBy(static point => point.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static DateOnly StartOfWeek(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }

    private static decimal Sum(IEnumerable<Contribution> rows, DateOnly start, DateOnly end) =>
        rows.Where(row => row.ReceivedDate >= start && row.ReceivedDate <= end).Sum(static row => row.TotalAmount);

    private static (DateOnly Start, DateOnly End) CurrentRange(OfferingPeriodGranularity granularity, DateOnly asOfDate) => granularity switch
    {
        OfferingPeriodGranularity.Week => (StartOfWeek(asOfDate), asOfDate),
        OfferingPeriodGranularity.Month => (new DateOnly(asOfDate.Year, asOfDate.Month, 1), asOfDate),
        OfferingPeriodGranularity.Year => (new DateOnly(asOfDate.Year, 1, 1), asOfDate),
        _ => (asOfDate, asOfDate)
    };

    private static IReadOnlyList<OfferingChartPoint> BuildWeekly(IReadOnlyList<Contribution> rows, DateOnly asOfDate, int count)
    {
        var finalWeek = StartOfWeek(asOfDate);
        var result = new List<OfferingChartPoint>(count);
        for (var i = count - 1; i >= 0; i--)
        {
            var start = finalWeek.AddDays(-7 * i);
            var end = start.AddDays(6);
            result.Add(new OfferingChartPoint(start.ToString("MMM d", System.Globalization.CultureInfo.InvariantCulture), Sum(rows, start, end)));
        }
        return result;
    }

    private static IReadOnlyList<OfferingChartPoint> BuildMonthly(IReadOnlyList<Contribution> rows, DateOnly asOfDate, int count)
    {
        var current = new DateOnly(asOfDate.Year, asOfDate.Month, 1);
        var result = new List<OfferingChartPoint>(count);
        for (var i = count - 1; i >= 0; i--)
        {
            var start = current.AddMonths(-i);
            var end = start.AddMonths(1).AddDays(-1);
            result.Add(new OfferingChartPoint(start.ToString("MMM yyyy", System.Globalization.CultureInfo.InvariantCulture), Sum(rows, start, end)));
        }
        return result;
    }

    private static IReadOnlyList<OfferingChartPoint> BuildYearly(IReadOnlyList<Contribution> rows, DateOnly asOfDate, int count)
    {
        var result = new List<OfferingChartPoint>(count);
        for (var year = asOfDate.Year - count + 1; year <= asOfDate.Year; year++)
        {
            var start = new DateOnly(year, 1, 1);
            var end = new DateOnly(year, 12, 31);
            result.Add(new OfferingChartPoint(year.ToString(System.Globalization.CultureInfo.InvariantCulture), Sum(rows, start, end)));
        }
        return result;
    }
}
