using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.AspNetCoreServer;
using Microsoft.AspNetCore.Http;
using PDS.Api.Security;

namespace PDS.Tests.Api;

public class AuthenticationHandlerTests
{
    [Fact]
    public void Gateway_builds_principal_from_authorizer_jwt_claims()
    {
        var context = new DefaultHttpContext();
        context.Items[AbstractAspNetCoreFunction.LAMBDA_REQUEST_OBJECT] = new APIGatewayHttpApiV2ProxyRequest
        {
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayHttpApiV2ProxyRequest.AuthorizerDescription
                {
                    Jwt = new APIGatewayHttpApiV2ProxyRequest.AuthorizerDescription.JwtDescription
                    {
                        Claims = new Dictionary<string, string>
                        {
                            ["sub"] = "abc",
                            ["email"] = "sam@example.com",
                            ["cognito:groups"] = "[Manager]",
                        },
                    },
                },
            },
        };

        var principal = GatewayAuthenticationHandler.PrincipalFromAuthorizer(context, "Gateway");

        Assert.NotNull(principal);
        Assert.True(principal.Identity!.IsAuthenticated);
        Assert.Equal("abc", principal.FindFirst("sub")?.Value);
        Assert.Equal("[Manager]", principal.FindFirst("cognito:groups")?.Value);
    }

    [Fact]
    public void Gateway_returns_null_without_authorizer_claims() =>
        Assert.Null(GatewayAuthenticationHandler.PrincipalFromAuthorizer(new DefaultHttpContext(), "Gateway"));

    [Fact]
    public void Development_user_carries_configured_roles_as_groups()
    {
        var principal = DevelopmentAuthenticationHandler.BuildPrincipal(
            new DevUserOptions { Id = "dev", Name = "Dev User", Email = "dev@example.com", Roles = ["Viewer"] },
            "Development");

        Assert.Equal("dev", principal.FindFirst("sub")?.Value);
        Assert.Equal("Dev User", principal.Identity?.Name);
        Assert.Equal(new[] { "Viewer" }, principal.FindAll("cognito:groups").Select(c => c.Value));
    }
}
