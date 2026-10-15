using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PDS.Shared.Settings;

public static class SettingsServiceCollectionExtensions
{
    /// <summary>Binds <see cref="LocaleOptions"/> and fails host startup when Locale:TimeZone does not resolve.</summary>
    public static IServiceCollection AddLocaleOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<LocaleOptions>()
            .Bind(configuration.GetSection(LocaleOptions.Section))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<LocaleOptions>, LocaleOptionsValidator>();
        return services;
    }
}

internal sealed class LocaleOptionsValidator : IValidateOptions<LocaleOptions>
{
    public ValidateOptionsResult Validate(string? name, LocaleOptions options) =>
        LocalDate.Validate(options.TimeZone) is { } error ? ValidateOptionsResult.Fail(error) : ValidateOptionsResult.Success;
}
