namespace PDS.Shared;

public sealed record Money
{
    public Money(decimal amount, string currency)
    {
        if (decimal.Round(amount, 2) != amount)
            throw new ArgumentException("Amount cannot have more than 2 decimal places.", nameof(amount));
        if (currency is not { Length: 3 } || !currency.All(char.IsAsciiLetterUpper))
            throw new ArgumentException("Currency must be a 3-letter upper-case ISO 4217 code.", nameof(currency));

        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }
}
