namespace ChurchBooks.Core.Finance;

public readonly record struct Money(decimal Amount, CurrencyCode Currency)
{
    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
        {
            throw new InvalidOperationException("Money values with different currencies cannot be combined without an explicit exchange rate.");
        }
    }
}
