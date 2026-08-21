using System.Globalization;
using ChurchBooks.App.Models;

namespace ChurchBooks.App.Services;

public static class CountryCurrencyCatalog
{
    private static readonly IReadOnlyList<CountryCurrencyOption> _options = BuildOptions();
    private static readonly IReadOnlyDictionary<string, CountryCurrencyOption> _byCode =
        _options.ToDictionary(item => item.CountryCode, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<CountryCurrencyOption> Options => _options;

    public static IReadOnlyList<string> CurrencyCodes { get; } = _options
        .Select(item => item.CurrencyCode)
        .Where(code => !string.IsNullOrWhiteSpace(code))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public static string? CurrencyForCountry(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode)) return null;
        return _byCode.TryGetValue(countryCode.Trim(), out var option)
            ? option.CurrencyCode
            : null;
    }

    private static IReadOnlyList<CountryCurrencyOption> BuildOptions()
    {
        var byCode = new Dictionary<string, CountryCurrencyOption>(StringComparer.OrdinalIgnoreCase);

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                var region = new RegionInfo(culture.Name);
                var code = region.TwoLetterISORegionName?.Trim().ToUpperInvariant();
                var currency = region.ISOCurrencySymbol?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(code) || code.Length != 2 ||
                    string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
                {
                    continue;
                }

                if (!byCode.ContainsKey(code))
                {
                    byCode[code] = new CountryCurrencyOption(code, region.EnglishName, currency);
                }
            }
            catch (ArgumentException)
            {
                // A few platform-specific cultures do not expose a usable RegionInfo.
            }
        }

        // Deterministic fallbacks for the countries most likely to be used with ChurchBooks.
        AddOrReplace(byCode, "PH", "Philippines", "PHP");
        AddOrReplace(byCode, "US", "United States", "USD");
        AddOrReplace(byCode, "CA", "Canada", "CAD");
        AddOrReplace(byCode, "GB", "United Kingdom", "GBP");
        AddOrReplace(byCode, "AU", "Australia", "AUD");
        AddOrReplace(byCode, "NZ", "New Zealand", "NZD");
        AddOrReplace(byCode, "SG", "Singapore", "SGD");
        AddOrReplace(byCode, "JP", "Japan", "JPY");
        AddOrReplace(byCode, "KR", "South Korea", "KRW");
        AddOrReplace(byCode, "IN", "India", "INR");
        AddOrReplace(byCode, "MY", "Malaysia", "MYR");
        AddOrReplace(byCode, "ID", "Indonesia", "IDR");
        AddOrReplace(byCode, "TH", "Thailand", "THB");
        AddOrReplace(byCode, "VN", "Vietnam", "VND");
        AddOrReplace(byCode, "AE", "United Arab Emirates", "AED");
        AddOrReplace(byCode, "SA", "Saudi Arabia", "SAR");

        return byCode.Values
            .OrderBy(item => item.CountryName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.CountryCode, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddOrReplace(
        IDictionary<string, CountryCurrencyOption> target,
        string countryCode,
        string countryName,
        string currencyCode)
    {
        target[countryCode] = new CountryCurrencyOption(countryCode, countryName, currencyCode);
    }
}
