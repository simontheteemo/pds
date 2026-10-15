namespace PDS.Consents.Domain;

internal sealed record ConditionFields(string? Reference, string Description, DateOnly? DueDate);
