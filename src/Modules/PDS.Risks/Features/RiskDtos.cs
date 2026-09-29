using PDS.Risks.Domain;

namespace PDS.Risks.Features;

/// <summary>Editable fields. Category, likelihood and impact are nullable so omission is a 400, not a default.</summary>
internal interface IRiskInput
{
    string Title { get; }

    RiskCategory? Category { get; }

    string? Description { get; }

    int? Likelihood { get; }

    int? Impact { get; }

    string? Mitigation { get; }

    string? Owner { get; }

    DateOnly? DueDate { get; }
}

internal sealed record CreateRiskRequest(
    string Title, RiskCategory? Category, string? Description, int? Likelihood, int? Impact,
    string? Mitigation, string? Owner, DateOnly? DueDate) : IRiskInput;

internal sealed record UpdateRiskRequest(
    string Title, RiskCategory? Category, string? Description, int? Likelihood, int? Impact,
    string? Mitigation, string? Owner, DateOnly? DueDate, RiskStatus? Status, long Version) : IRiskInput;

internal sealed record CloseRiskRequest(long Version, string Note);

internal sealed record ReopenRiskRequest(long Version);

internal static class RiskInputMapping
{
    /// <summary>Call only after validation.</summary>
    public static RiskFields ToFields(this IRiskInput input) => new(
        input.Title, input.Category!.Value, input.Description, input.Likelihood!.Value, input.Impact!.Value,
        input.Mitigation, input.Owner, input.DueDate);
}

internal sealed record RiskDetails(
    Guid Id,
    Guid ProjectId,
    string Title,
    RiskCategory Category,
    string? Description,
    int Likelihood,
    int Impact,
    int Score,
    RiskBand Band,
    string? Mitigation,
    string? Owner,
    DateOnly? DueDate,
    RiskStatus Status,
    string? ClosingNote,
    DateTimeOffset? ClosedAt,
    string? ClosedBy,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    long Version)
{
    public static RiskDetails From(Risk r)
    {
        var f = r.Fields;
        return new RiskDetails(
            r.Id.Value, r.ProjectId.Value, f.Title, f.Category, f.Description, f.Likelihood, f.Impact, r.Score, r.Band,
            f.Mitigation, f.Owner, f.DueDate, r.Status, r.ClosingNote, r.Closed?.At, r.Closed?.By,
            r.Created.At, r.Created.By, r.Updated.At, r.Updated.By, r.Version);
    }
}
