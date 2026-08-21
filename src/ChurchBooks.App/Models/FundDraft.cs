using ChurchBooks.Accounting.Funds;

namespace ChurchBooks.App.Models;

public sealed class FundDraft
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Purpose { get; init; } = string.Empty;
    public FundRestriction Restriction { get; init; } = FundRestriction.Unrestricted;
    public FundOverspendPolicy OverspendPolicy { get; init; } = FundOverspendPolicy.Allow;
}
