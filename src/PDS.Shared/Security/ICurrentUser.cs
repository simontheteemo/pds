namespace PDS.Shared.Security;

public interface ICurrentUser
{
    string Id { get; }

    string Name { get; }

    string? Email { get; }

    IReadOnlyList<string> Roles { get; }
}
