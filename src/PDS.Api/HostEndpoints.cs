using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using PDS.Shared.Settings;

namespace PDS.Api;

public sealed record ClientConfig(
    string ProductName, string? LogoUrl, string PrimaryColor, string Currency, string Culture, string TimeZone);

internal static class HostEndpoints
{
    public static IEndpointRouteBuilder MapHostEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/config", GetConfig).AllowAnonymous().WithName("GetConfig").WithTags("Host");
        return app;
    }

    internal static Ok<ClientConfig> GetConfig(IOptions<BrandingOptions> branding, IOptions<LocaleOptions> locale)
    {
        var b = branding.Value;
        var l = locale.Value;
        return TypedResults.Ok(new ClientConfig(b.ProductName, b.LogoUrl, b.PrimaryColor, l.Currency, l.Culture, l.TimeZone));
    }
}
