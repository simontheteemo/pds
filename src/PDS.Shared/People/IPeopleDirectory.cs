namespace PDS.Shared.People;

/// <summary>A user who can be assigned work. Id is the Cognito sub (equals ICurrentUser.Id).</summary>
public sealed record Person(string Id, string Name, string? Email, string Role);

/// <summary>Who can be assigned work. Host-provided (Cognito when deployed, configured list locally).</summary>
public interface IPeopleDirectory
{
    /// <summary>Everyone assignable, sorted by name (case-insensitive).</summary>
    Task<IReadOnlyList<Person>> ListAsync(CancellationToken ct);

    /// <summary>The person with this id, or null when they are not (or no longer) in the directory.</summary>
    Task<Person?> FindAsync(string id, CancellationToken ct);
}
