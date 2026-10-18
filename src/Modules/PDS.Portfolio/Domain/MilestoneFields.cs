namespace PDS.Portfolio.Domain;

internal sealed record MilestoneFields(
    string Name,
    MilestoneCategory Category,
    DateOnly? PlannedDate,
    DateOnly? ForecastDate,
    DateOnly? ActualDate,
    string? Owner,
    string? Notes,
    int SortOrder);
