using ChurchBooks.Accounting.Funds;

namespace ChurchBooks.App.Models;

public sealed record FundListItemViewModel(
    Guid Id,
    string Code,
    string Name,
    string Purpose,
    FundRestriction Restriction,
    FundOverspendPolicy OverspendPolicy,
    FundStatus Status,
    decimal Balance)
{
    public string RestrictionLabel => Restriction switch
    {
        FundRestriction.Unrestricted => "Unrestricted",
        FundRestriction.BoardDesignated => "Board designated",
        FundRestriction.DonorRestricted => "Donor restricted",
        FundRestriction.Endowment => "Endowment",
        _ => Restriction.ToString()
    };

    public string OverspendLabel => OverspendPolicy switch
    {
        FundOverspendPolicy.Allow => "Allow",
        FundOverspendPolicy.Warn => "Warn",
        FundOverspendPolicy.Block => "Block",
        _ => OverspendPolicy.ToString()
    };

    public string StatusLabel => Status == FundStatus.Active ? "Active" : "Archived";
    public string BalanceDisplay => $"PHP {Balance:N2}";
}
