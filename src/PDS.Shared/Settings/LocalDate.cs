using System.Collections.Concurrent;

namespace PDS.Shared.Settings;

public static class LocalDate
{
    private static readonly ConcurrentDictionary<string, TimeZoneInfo> Zones = new(StringComparer.Ordinal);

    /// <summary>Today's date in the deployment time zone (Locale:TimeZone, e.g. Pacific/Auckland).</summary>
    public static DateOnly Today(TimeProvider clock, LocaleOptions locale)
    {
        var zone = Zones.GetOrAdd(locale.TimeZone, static id => TimeZoneInfo.FindSystemTimeZoneById(id));
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
    }

    /// <summary>Resolves and caches the zone; returns an error message when it cannot be found.</summary>
    internal static string? Validate(string? timeZone)
    {
        if (string.IsNullOrWhiteSpace(timeZone))
            return "Locale:TimeZone is required (an IANA or Windows time zone id, e.g. Pacific/Auckland).";
        try
        {
            Zones.GetOrAdd(timeZone, static id => TimeZoneInfo.FindSystemTimeZoneById(id));
            return null;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return $"Locale:TimeZone '{timeZone}' could not be resolved on this host (is tzdata installed?): {ex.Message}";
        }
    }
}
