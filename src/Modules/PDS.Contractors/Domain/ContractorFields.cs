namespace PDS.Contractors.Domain;

internal sealed record ContractorFields(
    string Name,
    ContractorType Type,
    string? Trade,
    string? Nzbn,
    string? ContactName,
    string? Email,
    string? Phone,
    string? Address,
    DateOnly? PublicLiabilityExpiry,
    DateOnly? ProfessionalIndemnityExpiry,
    string? LicenceNumber,
    DateOnly? LicenceExpiry,
    string? Notes)
{
    public IEnumerable<DateOnly?> Expiries => [PublicLiabilityExpiry, ProfessionalIndemnityExpiry, LicenceExpiry];
}
