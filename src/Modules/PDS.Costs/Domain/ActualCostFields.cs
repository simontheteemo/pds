using PDS.Contractors.Contracts;
using PDS.Costs.Contracts;
using PDS.Shared;

namespace PDS.Costs.Domain;

internal sealed record ActualCostFields(
    BudgetLineId BudgetLineId,
    ActualCostType Type,
    ContractId? ContractId,
    ContractorId? ContractorId,
    string? Payee,
    string Description,
    string? Reference,
    DateOnly Date,
    DateOnly? DueDate,
    InvoiceStatus Status,
    DateOnly? PaidDate,
    Money Amount,
    Money? Gst,
    string? Notes);
