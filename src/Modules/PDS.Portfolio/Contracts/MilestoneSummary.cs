namespace PDS.Portfolio.Contracts;

/// <summary>A milestone as other modules see it. Status and category are the enum names; status is computed as of today.</summary>
public sealed record MilestoneSummary(
    MilestoneId Id,
    ProjectId ProjectId,
    string Name,
    string Category,
    DateOnly? PlannedDate,
    DateOnly? ForecastDate,
    DateOnly? ActualDate,
    string Status,
    int? SlippageDays,
    string? Owner,
    int SortOrder);
