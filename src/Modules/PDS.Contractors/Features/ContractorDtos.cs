using PDS.Contractors.Domain;

namespace PDS.Contractors.Features;

/// <summary>Editable fields. Type is nullable so omission is a 400, not a default.</summary>
internal interface IContractorInput
{
    string Name { get; }

    ContractorType? Type { get; }

    string? Trade { get; }

    string? Nzbn { get; }

    string? ContactName { get; }

    string? Email { get; }

    string? Phone { get; }

    string? Address { get; }

    DateOnly? PublicLiabilityExpiry { get; }

    DateOnly? ProfessionalIndemnityExpiry { get; }

    string? LicenceNumber { get; }

    DateOnly? LicenceExpiry { get; }

    string? Notes { get; }
}

internal sealed record CreateContractorRequest(
    string Name, ContractorType? Type, string? Trade, string? Nzbn, string? ContactName, string? Email,
    string? Phone, string? Address, DateOnly? PublicLiabilityExpiry, DateOnly? ProfessionalIndemnityExpiry,
    string? LicenceNumber, DateOnly? LicenceExpiry, string? Notes) : IContractorInput;

internal sealed record UpdateContractorRequest(
    string Name, ContractorType? Type, string? Trade, string? Nzbn, string? ContactName, string? Email,
    string? Phone, string? Address, DateOnly? PublicLiabilityExpiry, DateOnly? ProfessionalIndemnityExpiry,
    string? LicenceNumber, DateOnly? LicenceExpiry, string? Notes, long Version) : IContractorInput;

internal sealed record SetActiveRequest(long Version);

internal static class ContractorInputMapping
{
    /// <summary>Call only after validation.</summary>
    public static ContractorFields ToFields(this IContractorInput i) => new(
        i.Name, i.Type!.Value, i.Trade, i.Nzbn, i.ContactName, i.Email, i.Phone, i.Address,
        i.PublicLiabilityExpiry, i.ProfessionalIndemnityExpiry, i.LicenceNumber, i.LicenceExpiry, i.Notes);
}

internal sealed record ContractorListItem(
    Guid Id, string Name, ContractorType Type, string? Trade, ComplianceStatus Compliance, bool IsActive);

internal sealed record ContractorDetails(
    Guid Id,
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
    string? Notes,
    ComplianceStatus Compliance,
    ComplianceStatus PublicLiabilityStatus,
    ComplianceStatus ProfessionalIndemnityStatus,
    ComplianceStatus LicenceStatus,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    long Version)
{
    public static ContractorDetails From(Contractor c, DateOnly today)
    {
        var f = c.Fields;
        return new ContractorDetails(
            c.Id.Value, f.Name, f.Type, f.Trade, f.Nzbn, f.ContactName, f.Email, f.Phone, f.Address,
            f.PublicLiabilityExpiry, f.ProfessionalIndemnityExpiry, f.LicenceNumber, f.LicenceExpiry, f.Notes,
            c.ComplianceOn(today),
            Domain.Compliance.For(f.PublicLiabilityExpiry, today),
            Domain.Compliance.For(f.ProfessionalIndemnityExpiry, today),
            Domain.Compliance.For(f.LicenceExpiry, today),
            c.IsActive, c.Created.At, c.Created.By, c.Updated.At, c.Updated.By, c.Version);
    }
}
