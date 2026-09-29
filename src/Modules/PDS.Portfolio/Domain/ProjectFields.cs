using PDS.Shared;

namespace PDS.Portfolio.Domain;

/// <summary>Everything a user can edit on a project.</summary>
internal sealed record ProjectFields(
    string Code,
    string Name,
    Site Site,
    ProjectStage Stage,
    ProjectStatus Status,
    DateOnly? PlannedStart,
    DateOnly? PlannedCompletion,
    DateOnly? ActualStart,
    DateOnly? ActualCompletion,
    Money? Budget,
    string? ProjectManager,
    string? Description);
