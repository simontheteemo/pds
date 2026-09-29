using Amazon.DynamoDBv2.Model;
using PDS.Shared.Data;

namespace PDS.Tests.Shared;

public class AttrTests
{
    [Fact]
    public void Decimal_round_trips_through_number_attribute()
    {
        var item = new Dictionary<string, AttributeValue> { ["amount"] = Attr.N(12500000.50m) };

        Assert.Equal(12500000.50m, Attr.GetDecimalOrNull(item, "amount"));
    }

    [Fact]
    public void Date_round_trips_as_iso_date_string()
    {
        var item = new Dictionary<string, AttributeValue> { ["d"] = Attr.Date(new DateOnly(2026, 3, 1)) };

        Assert.Equal("2026-03-01", item["d"].S);
        Assert.Equal(new DateOnly(2026, 3, 1), Attr.GetDateOrNull(item, "d"));
    }

    [Fact]
    public void Timestamp_round_trips_in_utc()
    {
        var at = new DateTimeOffset(2026, 9, 29, 10, 15, 30, TimeSpan.FromHours(13));
        var item = new Dictionary<string, AttributeValue> { ["t"] = Attr.Timestamp(at) };

        var read = Attr.GetTimestamp(item, "t");

        Assert.Equal(at, read);
        Assert.Equal(TimeSpan.Zero, read.Offset);
    }

    [Fact]
    public void Missing_optional_attributes_read_as_null_or_false()
    {
        var item = new Dictionary<string, AttributeValue>();

        Assert.Null(Attr.GetStringOrNull(item, "x"));
        Assert.Null(Attr.GetDecimalOrNull(item, "x"));
        Assert.Null(Attr.GetDateOrNull(item, "x"));
        Assert.False(Attr.GetBool(item, "x"));
    }

    [Fact]
    public void Missing_required_attribute_throws_invalid_data()
    {
        var item = new Dictionary<string, AttributeValue>();

        Assert.Throws<InvalidDataException>(() => Attr.GetString(item, "code"));
        Assert.Throws<InvalidDataException>(() => Attr.GetLong(item, "version"));
    }

    [Fact]
    public void SetIfPresent_skips_null_and_empty_values()
    {
        var item = new Dictionary<string, AttributeValue>();

        Attr.SetIfPresent(item, "a", (string?)null);
        Attr.SetIfPresent(item, "b", "");
        Attr.SetIfPresent(item, "c", (decimal?)null);
        Attr.SetIfPresent(item, "d", (DateOnly?)null);
        Attr.SetIfPresent(item, "e", "kept");

        Assert.Equal(new[] { "e" }, item.Keys);
    }
}
