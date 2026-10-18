using Microsoft.Extensions.Options;
using PDS.Api.Security;
using PDS.Shared.People;
using PDS.Shared.Security;

namespace PDS.Api.People;

/// <summary>People from configuration (local development), plus the dev user when not already listed.</summary>
public sealed class ConfiguredPeopleDirectory(IOptions<PeopleOptions> options, IOptions<AuthOptions> auth) : IPeopleDirectory
{
    public Task<IReadOnlyList<Person>> ListAsync(CancellationToken ct) => Task.FromResult(Load());

    public Task<Person?> FindAsync(string id, CancellationToken ct) =>
        Task.FromResult(Load().FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal)));

    private IReadOnlyList<Person> Load()
    {
        var people = options.Value.Users
            .Select(u => new Person(u.Id, u.Name, u.Email, u.Role))
            .ToList();
        var dev = auth.Value.DevUser;
        if (!people.Any(p => string.Equals(p.Id, dev.Id, StringComparison.Ordinal)))
            people.Add(new Person(dev.Id, dev.Name, dev.Email, HighestRole(dev.Roles)));
        return people.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string HighestRole(string[] roles) =>
        Roles.All.FirstOrDefault(r => roles.Contains(r, StringComparer.OrdinalIgnoreCase)) ?? Roles.Viewer;
}
