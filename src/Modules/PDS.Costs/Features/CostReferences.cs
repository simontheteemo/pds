using Microsoft.AspNetCore.Http.HttpResults;
using PDS.Contractors.Contracts;
using PDS.Costs.Data;
using PDS.Portfolio.Contracts;

namespace PDS.Costs.Features;

internal static class CostReferences
{
    /// <summary>Null when valid; otherwise the 400 to return. Lines come from the already-loaded project data.</summary>
    public static async Task<ValidationProblem?> CheckAsync(
        ProjectId projectId, ProjectCostData data, Guid budgetLineId, Guid? contractId,
        IContractQueries contracts, CancellationToken ct)
    {
        if (!data.Lines.Any(l => l.Id.Value == budgetLineId))
            return CostsProblems.UnknownLine();
        if (contractId is { } cid &&
            !(await contracts.GetProjectContracts(projectId, ct)).Any(c => c.Id.Value == cid))
            return CostsProblems.UnknownContract();
        return null;
    }

    /// <summary>
    /// Null when valid; otherwise the 400 to return. Checks, in order: line, contract on the project, contractor exists,
    /// contract's contractor matches, contractor active (on create or when the contractor changes), and the invoice
    /// number is not already used by another entry of the same contractor (any project, any type).
    /// </summary>
    /// <param name="existingContractor">The entry's stored contractor on update; null on create.</param>
    /// <param name="selfId">The entry being updated, excluded from the duplicate check; null on create.</param>
    public static async Task<ValidationProblem?> CheckActualAsync(
        ProjectId projectId, ProjectCostData data, Guid budgetLineId, Guid? contractId, Guid? contractorId,
        ContractorId? existingContractor, Guid? selfId,
        IContractQueries contracts, IContractorQueries contractors, CostStore store, string? reference,
        CancellationToken ct)
    {
        if (!data.Lines.Any(l => l.Id.Value == budgetLineId))
            return CostsProblems.UnknownLine();

        ContractSummary? contract = null;
        if (contractId is { } cid)
        {
            contract = (await contracts.GetProjectContracts(projectId, ct)).FirstOrDefault(c => c.Id.Value == cid);
            if (contract is null)
                return CostsProblems.UnknownContract();
        }

        ContractorSummary? contractor = null;
        if (contractorId is { } kid)
        {
            contractor = (await contractors.GetContractors([new ContractorId(kid)], ct)).FirstOrDefault(c => c.Id.Value == kid);
            if (contractor is null)
                return CostsProblems.ContractorUnknown();
        }

        if (contract is not null && contract.ContractorId.Value != contractorId)
            return CostsProblems.ContractorMismatch();

        if (contractor is { IsActive: false } && (existingContractor is null || existingContractor != contractor.Id))
            return CostsProblems.ContractorInactive();

        var number = reference?.Trim();
        if (contractor is not null && !string.IsNullOrEmpty(number))
        {
            var existing = await store.ListActualsForContractorAsync(contractor.Id, ct);
            if (existing.Any(a => a.Id.Value != selfId &&
                                  string.Equals(a.Fields.Reference?.Trim(), number, StringComparison.OrdinalIgnoreCase)))
                return CostsProblems.DuplicateInvoiceNumber();
        }

        return null;
    }
}
