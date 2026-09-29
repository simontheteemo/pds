using System.Security.Claims;
using System.Text.Encodings.Web;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.AspNetCoreServer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace PDS.Api.Security;

/// <summary>
/// Trusts the claims that API Gateway's JWT authorizer has already validated. The Lambda adapter stores the
/// original request in HttpContext.Items; routes without an authorizer (GET /api/config) stay anonymous.
/// </summary>
internal sealed class GatewayAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var principal = PrincipalFromAuthorizer(Context, Scheme.Name);
        return Task.FromResult(principal is null
            ? AuthenticateResult.NoResult()
            : AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }

    internal static ClaimsPrincipal? PrincipalFromAuthorizer(HttpContext context, string scheme)
    {
        if (!context.Items.TryGetValue(AbstractAspNetCoreFunction.LAMBDA_REQUEST_OBJECT, out var raw)
            || raw is not APIGatewayHttpApiV2ProxyRequest request
            || request.RequestContext?.Authorizer?.Jwt?.Claims is not { Count: > 0 } claims)
        {
            return null;
        }

        var identity = new ClaimsIdentity(
            claims.Select(c => new Claim(c.Key, c.Value)), scheme, "name", ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }
}
