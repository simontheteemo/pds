namespace PDS.Portfolio.Contracts;

public sealed record ProjectSchedule(
    DateOnly? PlannedStart, DateOnly? PlannedCompletion, DateOnly? ActualStart, DateOnly? ActualCompletion);
