using PDS.Shared.Settings;

namespace PDS.Api;

public sealed class BrandingOptions
{
    public const string Section = "Branding";

    public string ProductName { get; set; } = "Property Development System";

    public string? LogoUrl { get; set; }

    public string PrimaryColor { get; set; } = "teal";
}

internal static class SettingsServiceCollectionExtensions
{
    public static IServiceCollection AddPdsSettings(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<BrandingOptions>().Bind(configuration.GetSection(BrandingOptions.Section));
        services.AddOptions<LocaleOptions>().Bind(configuration.GetSection(LocaleOptions.Section));
        return services;
    }
}
