using PDS.Actions.Domain;
using PDS.Shared.People;

namespace PDS.Actions.Features;

internal static class OwnerResolver
{
    /// <summary>
    /// The owner's id and display name, or null when <paramref name="ownerId"/> is not in the people directory.
    /// An unchanged owner keeps the stored name without a lookup, so actions owned by someone who has left stay editable.
    /// </summary>
    public static async Task<(string Id, string Name)?> ResolveAsync(
        IPeopleDirectory people, string ownerId, ActionFields? existing, CancellationToken ct)
    {
        if (existing is not null && string.Equals(existing.OwnerId, ownerId, StringComparison.Ordinal))
            return (existing.OwnerId, existing.OwnerName);

        return await people.FindAsync(ownerId, ct) is { } person ? (person.Id, person.Name) : null;
    }
}
