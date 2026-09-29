using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace PDS.Api.Security;

/// <summary>Signs every request in as the configured dev user. Refused at startup outside Development.</summary>
internal sealed class DevelopmentAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<AuthOptions> auth)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(BuildPrincipal(auth.Value.DevUser, Scheme.Name), Scheme.Name)));

    internal static ClaimsPrincipal BuildPrincipal(DevUserOptions user, string scheme)
    {
        var claims = new List<Claim> { new("sub", user.Id), new("name", user.Name) };
        if (user.Email is { Length: > 0 } email)
            claims.Add(new Claim("email", email));
        claims.AddRange(user.Roles.Select(r => new Claim(CognitoGroupsClaimsTransformation.GroupsClaim, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, scheme, "name", ClaimTypes.Role));
    }
}
