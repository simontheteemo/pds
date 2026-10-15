using PDS.Contractors.Contracts;
using PDS.Portfolio.Contracts;

namespace PDS.Contractors.Data;

internal sealed class ContractQueries(ContractStore contracts, ContractorStore contractors) : IContractQueries
{
    public async Task<IReadOnlyList<ContractSummary>> GetProjectContracts(ProjectId projectId, CancellationToken ct)
    {
        var list = await contracts.ListForProjectAsync(projectId, ct);
        if (list.Count == 0)
            return [];
        var names = (await contractors.GetManyAsync(list.Select(c => c.ContractorId).Distinct().ToList(), ct))
            .ToDictionary(c => c.Id, c => c.Fields.Name);
        return list
            .Select(c => new ContractSummary(
                c.Id, c.ProjectId, c.ContractorId,
                names.GetValueOrDefault(c.ContractorId, "(unknown contractor)"),
                c.Fields.Title, c.Fields.Reference, c.Fields.Status.ToString(),
                c.Fields.Value?.Amount, c.Fields.Value?.Currency))
            .ToList();
    }
}
