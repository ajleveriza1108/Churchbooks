using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.Funds;

namespace ChurchBooks.Accounting.Reporting;

public sealed class FundReportingService
{
    private readonly IFundAccountingStore _store;

    public FundReportingService(IFundAccountingStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<FundBalanceReport> BuildBalanceReportAsync(
        DateOnly asOfDate,
        bool includeArchived = true,
        CancellationToken cancellationToken = default)
    {
        var funds = await _store.GetAllFundsAsync(includeArchived, cancellationToken);
        var balances = await _store.GetFundBalancesAsOfAsync(
            funds.Select(static fund => fund.Id),
            asOfDate,
            cancellationToken);

        var rows = funds
            .Select(fund => new FundBalanceReportRow(
                fund.Id,
                fund.Code,
                fund.Name,
                fund.Restriction,
                fund.Status,
                balances.TryGetValue(fund.Id, out var balance) ? balance : 0m))
            .OrderBy(static row => row.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new FundBalanceReport(asOfDate, rows);
    }

    public async Task<FundActivityReport> BuildActivityReportAsync(
        Guid fundId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        if (fundId == Guid.Empty)
        {
            throw new ArgumentException("A valid fund is required.", nameof(fundId));
        }

        if (fromDate > toDate)
        {
            throw new ArgumentException("The report start date cannot be after the end date.", nameof(fromDate));
        }

        var fund = await _store.GetFundAsync(fundId, cancellationToken)
            ?? throw new InvalidOperationException("The selected fund no longer exists.");

        var openingBalance = 0m;
        if (fromDate > DateOnly.MinValue)
        {
            var openingLines = await _store.GetFundActivityAsync(
                fundId,
                toDate: fromDate.AddDays(-1),
                cancellationToken: cancellationToken);
            openingBalance = openingLines.Sum(static line => line.NetAssetImpact);
        }

        var lines = await _store.GetFundActivityAsync(fundId, fromDate, toDate, cancellationToken);
        var increases = lines.Where(static line => line.NetAssetImpact > 0m).Sum(static line => line.NetAssetImpact);
        var decreases = -lines.Where(static line => line.NetAssetImpact < 0m).Sum(static line => line.NetAssetImpact);
        var closingBalance = openingBalance + increases - decreases;

        return new FundActivityReport(
            fund,
            fromDate,
            toDate,
            openingBalance,
            increases,
            decreases,
            closingBalance,
            lines);
    }
}
