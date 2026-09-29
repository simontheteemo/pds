using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using PDS.Shared.Security;

namespace PDS.Api.Security;

/// <summary>
/// Maps cognito:groups to role claims. Groups arrive either as separate claims or, via API Gateway, as one
/// bracketed string such as "[Admin Manager]". Matching is case-insensitive; unknown groups are ignored.
/// </summary>
internal sealed class CognitoGroupsClaimsTransformation : IClaimsTransformation
{
    public const string GroupsClaim = "cognito:groups";
    private const string RolesIdentityType = "pds-roles";

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true
            || principal.Identities.Any(i => i.AuthenticationType == RolesIdentityType))
        {
            return Task.FromResult(principal);
        }

        var roles = ParseGroups(principal.FindAll(GroupsClaim).Select(c => c.Value))
            .Select(g => Roles.All.FirstOrDefault(r => string.Equals(r, g, StringComparison.OrdinalIgnoreCase)))
            .OfType<string>()
            .Distinct()
            .ToList();
        if (roles.Count == 0)
            return Task.FromResult(principal);

        var transformed = principal.Clone();
        transformed.AddIdentity(new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), RolesIdentityType));
        return Task.FromResult(transformed);
    }

    internal static IEnumerable<string> ParseGroups(IEnumerable<string> values) =>
        values.SelectMany(v => v.Trim().TrimStart('[').TrimEnd(']')
            .Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
