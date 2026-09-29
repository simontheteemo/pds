using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PDS.IntegrationTests;

/// <summary>
/// Signs in a test user when the X-Test-Role header is present. The roles are sent in API Gateway's bracketed
/// cognito:groups format, so the real claims transformation is exercised.
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string RoleHeader = "X-Test-Role";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RoleHeader, out var roles))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new[]
        {
            new Claim("sub", "test-user"),
            new Claim("name", "Test User"),
            new Claim("email", "test@example.com"),
            new Claim("cognito:groups", $"[{roles.ToString().Replace(',', ' ')}]"),
        };
        var identity = new ClaimsIdentity(claims, SchemeName, "name", ClaimTypes.Role);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
