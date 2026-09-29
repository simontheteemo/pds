namespace PDS.Shared.Settings;

public sealed class LocaleOptions
{
    public const string Section = "Locale";

    public string Currency { get; set; } = "NZD";

    public string Culture { get; set; } = "en-NZ";

    public string TimeZone { get; set; } = "Pacific/Auckland";
}
