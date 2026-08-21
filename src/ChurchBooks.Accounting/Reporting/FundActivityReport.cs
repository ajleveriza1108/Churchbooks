using ChurchBooks.Accounting.Funds;

namespace ChurchBooks.Accounting.Reporting;

public sealed record FundActivityReport(
    Fund Fund,
    DateOnly FromDate,
    DateOnly ToDate,
    decimal OpeningBalance,
    decimal Increases,
    decimal Decreases,
    decimal ClosingBalance,
    IReadOnlyList<FundActivityLine> Lines);
