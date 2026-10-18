using PDS.Contractors.Contracts;
using PDS.Costs.Contracts;
using PDS.Costs.Domain;
using PDS.Shared;

namespace PDS.Costs.Features;

/// <summary>Editable commitment fields. Nullable so omission is a 400, not a default.</summary>
internal interface ICommitmentInput
{
    Guid? BudgetLineId { get; }

    CommitmentType? Type { get; }

    Guid? ContractId { get; }

    string? Description { get; }

    string? Reference { get; }

    DateOnly? Date { get; }

    decimal? Amount { get; }

    string? Notes { get; }
}

/// <summary>Editable actual cost fields. Nullable so omission is a 400, not a default.</summary>
internal interface IActualCostInput
{
    Guid? BudgetLineId { get; }

    ActualCostType? Type { get; }

    Guid? ContractId { get; }

    Guid? ContractorId { get; }

    string? Payee { get; }

    string? Description { get; }

    string? Reference { get; }

    DateOnly? Date { get; }

    DateOnly? DueDate { get; }

    InvoiceStatus? Status { get; }

    DateOnly? PaidDate { get; }

    decimal? Amount { get; }

    decimal? GstAmount { get; }

    string? Notes { get; }
}

internal sealed record CreateCommitmentRequest(
    Guid? BudgetLineId, CommitmentType? Type, Guid? ContractId, string? Description, string? Reference,
    DateOnly? Date, decimal? Amount, string? Notes) : ICommitmentInput;

internal sealed record UpdateCommitmentRequest(
    Guid? BudgetLineId, CommitmentType? Type, Guid? ContractId, string? Description, string? Reference,
    DateOnly? Date, decimal? Amount, string? Notes, long Version) : ICommitmentInput;

internal sealed record CreateActualCostRequest(
    Guid? BudgetLineId, ActualCostType? Type, Guid? ContractId, Guid? ContractorId, string? Payee, string? Description,
    string? Reference, DateOnly? Date, DateOnly? DueDate, InvoiceStatus? Status, DateOnly? PaidDate, decimal? Amount,
    decimal? GstAmount, string? Notes) : IActualCostInput;

internal sealed record UpdateActualCostRequest(
    Guid? BudgetLineId, ActualCostType? Type, Guid? ContractId, Guid? ContractorId, string? Payee, string? Description,
    string? Reference, DateOnly? Date, DateOnly? DueDate, InvoiceStatus? Status, DateOnly? PaidDate, decimal? Amount,
    decimal? GstAmount, string? Notes, long Version) : IActualCostInput;

internal static class CostEntryInputMapping
{
    /// <summary>Call only after validation.</summary>
    public static CommitmentFields ToFields(this ICommitmentInput input, string currency) => new(
        new BudgetLineId(input.BudgetLineId!.Value),
        input.Type!.Value,
        input.ContractId is { } c ? new ContractId(c) : null,
        input.Description!,
        input.Reference,
        input.Date!.Value,
        new Money(input.Amount!.Value, currency),
        input.Notes);

    /// <summary>Call only after validation.</summary>
    public static ActualCostFields ToFields(this IActualCostInput input, string currency) => new(
        new BudgetLineId(input.BudgetLineId!.Value),
        input.Type!.Value,
        input.ContractId is { } c ? new ContractId(c) : null,
        input.ContractorId is { } k ? new ContractorId(k) : null,
        input.Payee,
        input.Description!,
        input.Reference,
        input.Date!.Value,
        input.DueDate,
        input.Status!.Value,
        input.PaidDate,
        new Money(input.Amount!.Value, currency),
        input.GstAmount is { } g ? new Money(g, currency) : null,
        input.Notes);
}

internal sealed record CommitmentDetails(
    Guid Id,
    Guid BudgetLineId,
    CommitmentType Type,
    Guid? ContractId,
    string Description,
    string? Reference,
    DateOnly Date,
    decimal Amount,
    string Currency,
    string? Notes,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    long Version)
{
    public static CommitmentDetails From(Commitment c)
    {
        var f = c.Fields;
        return new CommitmentDetails(
            c.Id.Value, f.BudgetLineId.Value, f.Type, f.ContractId?.Value, f.Description, f.Reference, f.Date,
            f.Amount.Amount, f.Amount.Currency, f.Notes,
            c.Created.At, c.Created.By, c.Updated.At, c.Updated.By, c.Version);
    }
}

internal sealed record ActualCostDetails(
    Guid Id,
    Guid BudgetLineId,
    ActualCostType Type,
    Guid? ContractId,
    Guid? ContractorId,
    string? Payee,
    string Description,
    string? Reference,
    DateOnly Date,
    DateOnly? DueDate,
    InvoiceStatus Status,
    DateOnly? PaidDate,
    decimal Amount,
    decimal? GstAmount,
    string Currency,
    string? Notes,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    long Version)
{
    public static ActualCostDetails From(ActualCost a)
    {
        var f = a.Fields;
        return new ActualCostDetails(
            a.Id.Value, f.BudgetLineId.Value, f.Type, f.ContractId?.Value, f.ContractorId?.Value, f.Payee, f.Description,
            f.Reference, f.Date, f.DueDate, f.Status, f.PaidDate, f.Amount.Amount, f.Gst?.Amount, f.Amount.Currency, f.Notes,
            a.Created.At, a.Created.By, a.Updated.At, a.Updated.By, a.Version);
    }
}
