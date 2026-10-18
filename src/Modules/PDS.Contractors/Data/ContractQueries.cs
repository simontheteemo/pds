using PDS.Contractors.Contracts;
using PDS.Contractors.Domain;
using PDS.Portfolio.Contracts;

namespace PDS.Contractors.Data;

internal sealed class ContractQueries(ContractStore contracts, ContractorStore contractors) : IContractQueries, IContractorQueries
{
    public async Task<IReadOnlyList<ContractSummary>> GetProjectContracts(ProjectId projectId, CancellationToken ct)
    {
        var list = await contracts.ListForProjectAsync(projectId, ct);
        if (list.Count == 0)
            return [];
        var names = (await contractors.GetManyAsync(list.Select(c => c.ContractorId).Distinct().ToList(), ct))
            .ToDictionary(c => c.Id, c => c.Fields.Name);
        return list
            .Select(c => ToSummary(c, names.GetValueOrDefault(c.ContractorId, "(unknown contractor)")))
            .ToList();
    }

    public async Task<IReadOnlyList<ContractSummary>> GetContractorContracts(ContractorId contractorId, CancellationToken ct)
    {
        var list = await contracts.ListForContractorAsync(contractorId, ct);
        if (list.Count == 0)
            return [];
        var found = await contractors.GetManyAsync([contractorId], ct);
        var name = found.Count > 0 ? found[0].Fields.Name : "(unknown contractor)";
        return list.Select(c => ToSummary(c, name)).ToList();
    }

    public async Task<IReadOnlyList<ContractorSummary>> GetContractors(IReadOnlyCollection<ContractorId> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
            return [];
        return (await contractors.GetManyAsync(ids, ct))
            .Select(c => new ContractorSummary(c.Id, c.Fields.Name, c.IsActive))
            .ToList();
    }

    private static ContractSummary ToSummary(Contract c, string contractorName) =>
        new(c.Id, c.ProjectId, c.ContractorId, contractorName,
            c.Fields.Title, c.Fields.Reference, c.Fields.Status.ToString(),
            c.Fields.Value?.Amount, c.Fields.Value?.Currency);
}
