using System.Security.Claims;
using PDS.Api.Security;

namespace PDS.Tests.Api;

public class CognitoGroupsClaimsTransformationTests
{
    private static ClaimsPrincipal Authenticated(params string[] groupClaimValues) =>
        new(new ClaimsIdentity(
            groupClaimValues.Select(v => new Claim("cognito:groups", v)).Append(new Claim("sub", "u1")),
            "Test", "name", ClaimTypes.Role));

    private static Task<ClaimsPrincipal> Transform(ClaimsPrincipal principal) =>
        new CognitoGroupsClaimsTransformation().TransformAsync(principal);

    [Fact]
    public async Task Maps_separate_group_claims_to_roles()
    {
        var user = await Transform(Authenticated("Admin", "Viewer"));

        Assert.True(user.IsInRole("Admin"));
        Assert.True(user.IsInRole("Viewer"));
        Assert.False(user.IsInRole("Manager"));
    }

    [Fact]
    public async Task Maps_api_gateway_bracketed_group_string_to_roles()
    {
        var user = await Transform(Authenticated("[Manager Viewer]"));

        Assert.True(user.IsInRole("Manager"));
        Assert.True(user.IsInRole("Viewer"));
    }

    [Fact]
    public async Task Maps_group_names_case_insensitively_to_canonical_roles()
    {
        var user = await Transform(Authenticated("[manager]"));

        Assert.True(user.IsInRole("Manager"));
    }

    [Fact]
    public async Task Ignores_unknown_groups()
    {
        var user = await Transform(Authenticated("[Accounting]"));

        Assert.DoesNotContain(user.Claims, c => c.Type == ClaimTypes.Role);
    }

    [Fact]
    public async Task Is_idempotent()
    {
        var once = await Transform(Authenticated("[Admin]"));
        var twice = await Transform(once);

        Assert.Single(twice.Claims, c => c.Type == ClaimTypes.Role);
    }

    [Fact]
    public async Task Leaves_anonymous_users_unchanged()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await Transform(anonymous);

        Assert.Same(anonymous, result);
    }
}
