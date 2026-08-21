using Xunit;
using ChurchBooks.Core.Finance;

namespace ChurchBooks.Core.Tests;

public sealed class MoneyTests
{
    [Fact]
    public void Add_SameCurrency_AddsAmounts()
    {
        var first = new Money(100m, CurrencyCode.Php);
        var second = new Money(25.50m, CurrencyCode.Php);

        var result = first.Add(second);

        Assert.Equal(125.50m, result.Amount);
        Assert.Equal(CurrencyCode.Php, result.Currency);
    }

    [Fact]
    public void Add_DifferentCurrency_RequiresExplicitConversion()
    {
        var php = new Money(100m, CurrencyCode.Php);
        var usd = new Money(2m, CurrencyCode.Usd);

        Assert.Throws<InvalidOperationException>(() => php.Add(usd));
    }

    [Theory]
    [InlineData("php", "PHP")]
    [InlineData(" usd ", "USD")]
    public void CurrencyCode_NormalizesAsciiCode(string raw, string expected)
    {
        Assert.Equal(expected, new CurrencyCode(raw).Value);
    }
}
