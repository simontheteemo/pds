using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Microsoft.Extensions.Options;
using PDS.Shared.People;
using PDS.Shared.Security;

namespace PDS.Api.People;

/// <summary>People from the Cognito user pool's role groups. Highest role wins when a user is in several groups.</summary>
public sealed class CognitoPeopleDirectory(IAmazonCognitoIdentityProvider cognito, IOptions<PeopleOptions> options) : IPeopleDirectory
{
    public async Task<IReadOnlyList<Person>> ListAsync(CancellationToken ct)
    {
        var bySub = new Dictionary<string, Person>(StringComparer.Ordinal);
        foreach (var role in new[] { Roles.Admin, Roles.Manager, Roles.Viewer })
        {
            string? next = null;
            do
            {
                var page = await cognito.ListUsersInGroupAsync(
                    new ListUsersInGroupRequest
                    {
                        UserPoolId = options.Value.UserPoolId,
                        GroupName = role,
                        Limit = 60,
                        NextToken = next,
                    },
                    ct);
                foreach (var user in page.Users ?? [])
                {
                    if (user.Enabled != true)
                        continue;
                    var sub = Attribute(user, "sub");
                    if (sub is null || bySub.ContainsKey(sub))
                        continue;
                    var email = Attribute(user, "email");
                    var name = Attribute(user, "name") ?? email ?? user.Username;
                    bySub[sub] = new Person(sub, name, email, role);
                }

                next = page.NextToken;
            }
            while (!string.IsNullOrEmpty(next));
        }

        return bySub.Values.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<Person?> FindAsync(string id, CancellationToken ct) =>
        (await ListAsync(ct)).FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));

    private static string? Attribute(UserType user, string name)
    {
        var value = user.Attributes?.FirstOrDefault(a => a.Name == name)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
