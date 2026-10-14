using PDS.Shared;

namespace PDS.Contractors.Domain;

internal sealed record ContractFields(
    string Title,
    string? Reference,
    ContractForm Form,
    Money? Value,
    DateOnly? StartDate,
    DateOnly? EndDate,
    decimal? RetentionPercent,
    ContractStatus Status,
    string? Notes);
