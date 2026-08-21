using ChurchBooks.Core.Finance;

namespace ChurchBooks.Accounting.Setup;

public sealed record OrganizationProfile
{
    public string DisplayName { get; }
    public string LegalName { get; }
    public CurrencyCode BaseCurrency { get; }
    public int FiscalYearStartMonth { get; }
    public string CountryCode { get; }
    public string TaxIdentifier { get; }
    public bool SetupComplete { get; }
    public DateTimeOffset UpdatedUtc { get; }

    public OrganizationProfile(
        string displayName,
        string legalName,
        CurrencyCode baseCurrency,
        int fiscalYearStartMonth,
        string countryCode,
        string taxIdentifier = "",
        bool setupComplete = false,
        DateTimeOffset? updatedUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (fiscalYearStartMonth is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(fiscalYearStartMonth));
        DisplayName = displayName.Trim();
        LegalName = string.IsNullOrWhiteSpace(legalName) ? DisplayName : legalName.Trim();
        BaseCurrency = baseCurrency;
        FiscalYearStartMonth = fiscalYearStartMonth;
        CountryCode = string.IsNullOrWhiteSpace(countryCode) ? "PH" : countryCode.Trim().ToUpperInvariant();
        TaxIdentifier = taxIdentifier?.Trim() ?? string.Empty;
        SetupComplete = setupComplete;
        UpdatedUtc = updatedUtc ?? DateTimeOffset.UtcNow;
    }
}
