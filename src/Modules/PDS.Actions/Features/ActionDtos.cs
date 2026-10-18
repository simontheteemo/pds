using PDS.Actions.Domain;

namespace PDS.Actions.Features;

internal sealed record ActionLinkDto(ActionLinkedModule? Module, Guid RecordId, string Label);

/// <summary>Editable fields. OwnerId and Priority are nullable so omission is a 400 (owner) or a default (priority).</summary>
internal interface IActionInput
{
    string Title { get; }

    string? Description { get; }

    string? OwnerId { get; }

    DateOnly? DueDate { get; }

    ActionPriority? Priority { get; }

    ActionLinkDto? Link { get; }
}

internal sealed record CreateActionRequest(
    string Title, string? Description, string? OwnerId, DateOnly? DueDate, ActionPriority? Priority,
    ActionLinkDto? Link) : IActionInput;

internal sealed record UpdateActionRequest(
    string Title, string? Description, string? OwnerId, DateOnly? DueDate, ActionPriority? Priority,
    ActionLinkDto? Link, ActionStatus? Status, long Version) : IActionInput;

/// <summary>Body of complete, cancel and reopen.</summary>
internal sealed record ActionVersionRequest(long Version);

internal static class ActionInputMapping
{
    /// <summary>Call only after validation and owner resolution.</summary>
    public static ActionFields ToFields(this IActionInput input, string ownerId, string ownerName) => new(
        input.Title, input.Description, ownerId, ownerName, input.DueDate, input.Priority ?? ActionPriority.Normal,
        input.Link is { } link ? new ActionLink(link.Module, link.RecordId, link.Label) : null);
}

internal sealed record ActionDetails(
    Guid Id,
    Guid ProjectId,
    string Title,
    string? Description,
    string OwnerId,
    string OwnerName,
    DateOnly? DueDate,
    ActionPriority Priority,
    ActionStatus Status,
    bool IsOverdue,
    ActionLinkDto? Link,
    DateTimeOffset? CompletedAt,
    string? CompletedBy,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    long Version)
{
    public static ActionDetails From(ActionItem a, DateOnly today)
    {
        var f = a.Fields;
        return new ActionDetails(
            a.Id.Value, a.ProjectId.Value, f.Title, f.Description, f.OwnerId, f.OwnerName, f.DueDate, f.Priority,
            a.Status, a.IsOverdue(today),
            f.Link is { } link ? new ActionLinkDto(link.Module, link.RecordId, link.Label) : null,
            a.Completed?.At, a.Completed?.By, a.Created.At, a.Created.By, a.Updated.At, a.Updated.By, a.Version);
    }
}
