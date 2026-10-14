using PDS.Contractors.Contracts;
using PDS.Contractors.Domain;
using PDS.Shared;

namespace PDS.Contractors.Features;

/// <summary>Editable fields. Form and Status are nullable so omission is a 400, not a default.</summary>
internal interface IContractInput
{
    Guid? ContractorId { get; }

    string Title { get; }

    string? Reference { get; }

    ContractForm? Form { get; }

    decimal? ValueAmount { get; }

    DateOnly? StartDate { get; }

    DateOnly? EndDate { get; }

    decimal? RetentionPercent { get; }

    ContractStatus? Status { get; }

    string? Notes { get; }
}

internal sealed record CreateContractRequest(
    Guid? ContractorId, string Title, string? Reference, ContractForm? Form, decimal? ValueAmount,
    DateOnly? StartDate, DateOnly? EndDate, decimal? RetentionPercent, ContractStatus? Status, string? Notes)
    : IContractInput;

internal sealed record UpdateContractRequest(
    Guid? ContractorId, string Title, string? Reference, ContractForm? Form, decimal? ValueAmount,
    DateOnly? StartDate, DateOnly? EndDate, decimal? RetentionPercent, ContractStatus? Status, string? Notes,
    long Version) : IContractInput;

internal static class ContractInputMapping
{
    /// <summary>Call only after validation.</summary>
    public static ContractFields ToFields(this IContractInput i, string currency) => new(
        i.Title, i.Reference, i.Form!.Value, i.ValueAmount is { } v ? new Money(v, currency) : null,
        i.StartDate, i.EndDate, i.RetentionPercent, i.Status!.Value, i.Notes);
}

internal sealed record ContractListItem(
    Guid Id,
    Guid ProjectId,
    string? ProjectCode,
    string? ProjectName,
    Guid ContractorId,
    string ContractorName,
    string Title,
    string? Reference,
    ContractForm Form,
    decimal? ValueAmount,
    string Currency,
    DateOnly? StartDate,
    DateOnly? EndDate,
    ContractStatus Status,
    long Version);

internal sealed record ContractDetails(
    Guid Id,
    Guid ProjectId,
    Guid ContractorId,
    string ContractorName,
    string Title,
    string? Reference,
    ContractForm Form,
    decimal? ValueAmount,
    string Currency,
    DateOnly? StartDate,
    DateOnly? EndDate,
    decimal? RetentionPercent,
    ContractStatus Status,
    string? Notes,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    long Version)
{
    public static ContractDetails From(Contract c, string contractorName, string defaultCurrency)
    {
        var f = c.Fields;
        return new ContractDetails(
            c.Id.Value, c.ProjectId.Value, c.ContractorId.Value, contractorName, f.Title, f.Reference, f.Form,
            f.Value?.Amount, f.Value?.Currency ?? defaultCurrency, f.StartDate, f.EndDate, f.RetentionPercent, f.Status, f.Notes,
            c.Created.At, c.Created.By, c.Updated.At, c.Updated.By, c.Version);
    }
}

internal static class ContractListing
{
    public const string UnknownContractor = "(unknown contractor)";

    public static ContractListItem ToListItem(
        Contract c, string contractorName, string? projectCode, string? projectName, string defaultCurrency) =>
        new(c.Id.Value, c.ProjectId.Value, projectCode, projectName, c.ContractorId.Value, contractorName,
            c.Fields.Title, c.Fields.Reference, c.Fields.Form, c.Fields.Value?.Amount, c.Fields.Value?.Currency ?? defaultCurrency,
            c.Fields.StartDate, c.Fields.EndDate, c.Fields.Status, c.Version);
}
