using PDS.Consents.Data;
using PDS.Consents.Domain;

namespace PDS.Consents.Features;

/// <summary>Editable fields. Type and status are nullable so omission is a 400, not a default.</summary>
internal interface IConsentInput
{
    ConsentType? Type { get; }

    string Title { get; }

    string? Authority { get; }

    string? Reference { get; }

    ConsentStatus? Status { get; }

    DateOnly? LodgedDate { get; }

    DateOnly? GrantedDate { get; }

    DateOnly? ExpiryDate { get; }

    string? Notes { get; }
}

internal sealed record CreateConsentRequest(
    ConsentType? Type, string Title, string? Authority, string? Reference, ConsentStatus? Status,
    DateOnly? LodgedDate, DateOnly? GrantedDate, DateOnly? ExpiryDate, string? Notes) : IConsentInput;

internal sealed record UpdateConsentRequest(
    ConsentType? Type, string Title, string? Authority, string? Reference, ConsentStatus? Status,
    DateOnly? LodgedDate, DateOnly? GrantedDate, DateOnly? ExpiryDate, string? Notes, long Version) : IConsentInput;

internal static class ConsentInputMapping
{
    /// <summary>Call only after validation.</summary>
    public static ConsentFields ToFields(this IConsentInput i) => new(
        i.Type!.Value, i.Title, i.Authority, i.Reference, i.Status!.Value, i.LodgedDate, i.GrantedDate, i.ExpiryDate, i.Notes);
}

internal sealed record ConditionDetails(
    Guid Id,
    Guid ProjectId,
    Guid ConsentId,
    string? Reference,
    string Description,
    DateOnly? DueDate,
    DueState DueState,
    DateOnly? CompletedOn,
    DateTimeOffset? CompletedAt,
    string? CompletedBy,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    long Version)
{
    public static ConditionDetails From(Condition c, bool consentActive, DateOnly today) => new(
        c.Id.Value, c.ProjectId.Value, c.ConsentId.Value, c.Fields.Reference, c.Fields.Description, c.Fields.DueDate,
        ConsentRules.ConditionState(c, consentActive, today), c.CompletedOn, c.Completed?.At, c.Completed?.By,
        c.Created.At, c.Created.By, c.Updated.At, c.Updated.By, c.Version);
}

internal sealed record ConsentDetails(
    Guid Id,
    Guid ProjectId,
    ConsentType Type,
    string Title,
    string? Authority,
    string? Reference,
    ConsentStatus Status,
    DateOnly? LodgedDate,
    DateOnly? GrantedDate,
    DateOnly? ExpiryDate,
    string? Notes,
    bool IsActive,
    DueState ExpiryState,
    DueState Attention,
    int ConditionCount,
    int OpenConditionCount,
    int OverdueConditionCount,
    int DueSoonConditionCount,
    List<ConditionDetails> Conditions,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    long Version)
{
    public static ConsentDetails From(ConsentWithConditions record, DateOnly today)
    {
        var consent = record.Consent;
        var conditions = record.Conditions
            .Select(c => ConditionDetails.From(c, consent.IsActive, today))
            .OrderBy(c => c.CompletedOn is not null)
            .ThenBy(c => c.DueDate is null)
            .ThenBy(c => c.DueDate)
            .ThenBy(c => c.Reference, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var expiryState = ConsentRules.ExpiryState(consent, today);
        var attention = consent.IsActive
            ? ConsentRules.Worst([expiryState, .. conditions.Select(c => c.DueState)])
            : DueState.None;
        var f = consent.Fields;
        return new ConsentDetails(
            consent.Id.Value, consent.ProjectId.Value, f.Type, f.Title, f.Authority, f.Reference, f.Status,
            f.LodgedDate, f.GrantedDate, f.ExpiryDate, f.Notes, consent.IsActive, expiryState, attention,
            conditions.Count,
            conditions.Count(c => c.CompletedOn is null),
            conditions.Count(c => c.DueState == DueState.Overdue),
            conditions.Count(c => c.DueState == DueState.DueSoon),
            conditions,
            consent.Created.At, consent.Created.By, consent.Updated.At, consent.Updated.By, consent.Version);
    }
}
