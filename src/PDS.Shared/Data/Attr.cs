using System.Globalization;
using Amazon.DynamoDBv2.Model;

namespace PDS.Shared.Data;

/// <summary>Reads and writes DynamoDB attribute values. Missing optional attributes read as null (or false).</summary>
public static class Attr
{
    private const string DateFormat = "yyyy-MM-dd";

    public static AttributeValue S(string value) => new() { S = value };

    public static AttributeValue N(decimal value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };

    public static AttributeValue N(long value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };

    public static AttributeValue Bool(bool value) => new() { BOOL = value };

    public static AttributeValue Map(Dictionary<string, AttributeValue> value) => new() { M = value };

    public static AttributeValue Date(DateOnly value) => S(value.ToString(DateFormat, CultureInfo.InvariantCulture));

    public static AttributeValue Timestamp(DateTimeOffset value) =>
        S(value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

    public static void SetIfPresent(Dictionary<string, AttributeValue> item, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            item[key] = S(value);
    }

    public static void SetIfPresent(Dictionary<string, AttributeValue> item, string key, decimal? value)
    {
        if (value is { } v)
            item[key] = N(v);
    }

    public static void SetIfPresent(Dictionary<string, AttributeValue> item, string key, DateOnly? value)
    {
        if (value is { } v)
            item[key] = Date(v);
    }

    public static string GetString(IReadOnlyDictionary<string, AttributeValue> item, string key) =>
        GetStringOrNull(item, key) ?? throw Missing(key);

    public static string? GetStringOrNull(IReadOnlyDictionary<string, AttributeValue> item, string key) =>
        item.TryGetValue(key, out var value) ? value.S : null;

    public static decimal? GetDecimalOrNull(IReadOnlyDictionary<string, AttributeValue> item, string key) =>
        item.TryGetValue(key, out var value) && value.N is { } n
            ? decimal.Parse(n, NumberStyles.Float, CultureInfo.InvariantCulture)
            : null;

    public static long GetLong(IReadOnlyDictionary<string, AttributeValue> item, string key) =>
        item.TryGetValue(key, out var value) && value.N is { } n
            ? long.Parse(n, NumberStyles.Integer, CultureInfo.InvariantCulture)
            : throw Missing(key);

    public static bool GetBool(IReadOnlyDictionary<string, AttributeValue> item, string key) =>
        item.TryGetValue(key, out var value) && value.BOOL == true;

    public static DateOnly? GetDateOrNull(IReadOnlyDictionary<string, AttributeValue> item, string key) =>
        GetStringOrNull(item, key) is { } s ? DateOnly.ParseExact(s, DateFormat, CultureInfo.InvariantCulture) : null;

    public static DateTimeOffset GetTimestamp(IReadOnlyDictionary<string, AttributeValue> item, string key) =>
        DateTimeOffset.Parse(GetString(item, key), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public static IReadOnlyDictionary<string, AttributeValue> GetMap(IReadOnlyDictionary<string, AttributeValue> item, string key) =>
        item.TryGetValue(key, out var value) && value.M is { } map ? map : throw Missing(key);

    private static InvalidDataException Missing(string key) => new($"Item is missing required attribute '{key}'.");
}
