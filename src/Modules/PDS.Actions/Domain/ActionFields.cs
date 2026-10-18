namespace PDS.Actions.Domain;

/// <summary>Everything a user edits on an action (status is changed separately). OwnerName is resolved, not typed.</summary>
internal sealed record ActionFields(
    string Title,
    string? Description,
    string OwnerId,
    string OwnerName,
    DateOnly? DueDate,
    ActionPriority Priority,
    ActionLink? Link);
