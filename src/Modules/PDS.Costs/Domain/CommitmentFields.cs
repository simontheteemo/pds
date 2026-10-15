using PDS.Contractors.Contracts;
using PDS.Costs.Contracts;
using PDS.Shared;

namespace PDS.Costs.Domain;

internal sealed record CommitmentFields(
    BudgetLineId BudgetLineId,
    CommitmentType Type,
    ContractId? ContractId,
    string Description,
    string? Reference,
    DateOnly Date,
    Money Amount,
    string? Notes);
