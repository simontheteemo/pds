using System.Security.Claims;
using PDS.Shared.Security;

namespace PDS.Api.Security;

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal User => accessor.HttpContext?.User ?? new ClaimsPrincipal();

    public string Id => User.FindFirstValue("sub") ?? "anonymous";

    public string Name =>
        User.FindFirstValue("name") ?? User.FindFirstValue("email") ?? User.FindFirstValue("cognito:username") ?? Id;

    public string? Email => User.FindFirstValue("email");

    public IReadOnlyList<string> Roles => PDS.Shared.Security.Roles.All.Where(User.IsInRole).ToArray();
}
