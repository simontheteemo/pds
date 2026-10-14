namespace PDS.Risks.Domain;

/// <summary>Everything a user edits on a risk (status is changed separately).</summary>
internal sealed record RiskFields(
    string Title,
    RiskCategory Category,
    string? Description,
    int Likelihood,
    int Impact,
    string? Mitigation,
    string? Owner,
    DateOnly? DueDate);
