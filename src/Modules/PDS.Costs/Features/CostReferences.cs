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
}
