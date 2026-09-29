using PDS.Shared;

namespace PDS.Tests.Shared;

public class MoneyTests
{
    [Fact]
    public void Accepts_amounts_with_two_decimal_places()
    {
        var money = new Money(1234.56m, "NZD");

        Assert.Equal(1234.56m, money.Amount);
        Assert.Equal("NZD", money.Currency);
    }

    [Fact]
    public void Rejects_more_than_two_decimal_places() =>
        Assert.Throws<ArgumentException>(() => new Money(1.005m, "NZD"));

    [Theory]
    [InlineData("nzd")]
    [InlineData("NZ")]
    [InlineData("NZDD")]
    [InlineData("")]
    public void Rejects_invalid_currency_codes(string currency) =>
        Assert.Throws<ArgumentException>(() => new Money(1m, currency));

    [Fact]
    public void Amounts_with_different_scale_are_equal() =>
        Assert.Equal(new Money(1.1m, "NZD"), new Money(1.10m, "NZD"));
}
