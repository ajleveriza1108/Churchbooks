using System.Globalization;

namespace ChurchBooks.Core.Finance;

public readonly record struct CurrencyCode
{
    public string Value { get; }

    public CurrencyCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length != 3 || normalized.Any(static c => c < 'A' || c > 'Z'))
        {
            throw new ArgumentException("Currency code must contain exactly three ASCII letters.", nameof(value));
        }

        Value = normalized;
    }

    public override string ToString() => Value;

    public static CurrencyCode Php => new("PHP");
    public static CurrencyCode Usd => new("USD");
}
