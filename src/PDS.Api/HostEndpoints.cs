using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using PDS.Api.Security;
using PDS.Shared.Security;
using PDS.Shared.Settings;

namespace PDS.Api;

public sealed record ClientAuthConfig(AuthMode Mode, string? Authority, string? ClientId, string? LogoutDomain);

public sealed record ClientConfig(
    string ProductName,
    string? LogoUrl,
    string PrimaryColor,
    string Currency,
    string Culture,
    string TimeZone,
    ClientAuthConfig Auth);

public sealed record Me(string Id, string Name, string? Email, string[] Roles);

internal static class HostEndpoints
{
    public static IEndpointRouteBuilder MapHostEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/config", GetConfig).AllowAnonymous().WithName("GetConfig").WithTags("Host");
        app.MapGet("/api/me", GetMe).RequireAuthorization(Policies.CanRead).WithName("GetMe").WithTags("Host");
        return app;
    }

    internal static Ok<ClientConfig> GetConfig(
        IOptions<BrandingOptions> branding, IOptions<LocaleOptions> locale, IOptions<AuthOptions> auth)
    {
        var b = branding.Value;
        var l = locale.Value;
        var a = auth.Value;
        return TypedResults.Ok(new ClientConfig(
            b.ProductName, b.LogoUrl, b.PrimaryColor, l.Currency, l.Culture, l.TimeZone,
            new ClientAuthConfig(a.Mode, a.Authority, a.ClientId, a.LogoutDomain)));
    }

    internal static Ok<Me> GetMe(ICurrentUser user) =>
        TypedResults.Ok(new Me(user.Id, user.Name, user.Email, [.. user.Roles]));
}
