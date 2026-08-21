using ChurchBooks.Accounting.Funds;

namespace ChurchBooks.Accounting.Reporting;

public sealed record FundBalanceReportRow(
    Guid FundId,
    string Code,
    string Name,
    FundRestriction Restriction,
    FundStatus Status,
    decimal Balance)
{
    public bool IsRestricted => Restriction is FundRestriction.DonorRestricted or FundRestriction.Endowment;
}
