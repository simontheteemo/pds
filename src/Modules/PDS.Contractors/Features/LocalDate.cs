using PDS.Shared.Settings;

namespace PDS.Contractors.Features;

internal static class LocalDate
{
    /// <summary>Today's date in the deployment time zone (Locale:TimeZone, e.g. Pacific/Auckland).</summary>
    public static DateOnly Today(TimeProvider clock, LocaleOptions locale)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(locale.TimeZone);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
    }
}
