using PDS.Consents.Domain;

namespace PDS.Consents.Features;

internal interface IConditionInput
{
    string? Reference { get; }

    string Description { get; }

    DateOnly? DueDate { get; }
}

internal sealed record CreateConditionRequest(string? Reference, string Description, DateOnly? DueDate) : IConditionInput;

internal sealed record UpdateConditionRequest(string? Reference, string Description, DateOnly? DueDate, long Version) : IConditionInput;

internal sealed record CompleteConditionRequest(long Version, DateOnly? CompletedOn);

internal sealed record ReopenConditionRequest(long Version);

internal static class ConditionInputMapping
{
    public static ConditionFields ToFields(this IConditionInput i) => new(i.Reference, i.Description, i.DueDate);
}

internal enum DueItemKind
{
    Condition,
    Expiry,
}

internal sealed record DueItem(
    DueItemKind Kind,
    Guid ProjectId,
    string ProjectCode,
    string ProjectName,
    Guid ConsentId,
    ConsentType ConsentType,
    string ConsentTitle,
    string? ConsentReference,
    Guid? ConditionId,
    string? ConditionReference,
    string Description,
    DateOnly Date,
    DueState State);
