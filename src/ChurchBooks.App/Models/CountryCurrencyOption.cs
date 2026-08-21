namespace ChurchBooks.App.Models;

public sealed record CountryCurrencyOption(
    string CountryCode,
    string CountryName,
    string CurrencyCode)
{
    public string DisplayName => $"{CountryName} ({CountryCode})";
}
