namespace PDS.Consents.Domain;

internal sealed record ConsentFields(
    ConsentType Type, string Title, string? Authority, string? Reference, ConsentStatus Status,
    DateOnly? LodgedDate, DateOnly? GrantedDate, DateOnly? ExpiryDate, string? Notes);
