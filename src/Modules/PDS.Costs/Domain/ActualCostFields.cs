using PDS.Contractors.Contracts;
using PDS.Costs.Contracts;
using PDS.Shared;

namespace PDS.Costs.Domain;

internal sealed record ActualCostFields(
    BudgetLineId BudgetLineId,
    ActualCostType Type,
    ContractId? ContractId,
    string? Payee,
    string Description,
    string? Reference,
    DateOnly Date,
    Money Amount,
    string? Notes);
