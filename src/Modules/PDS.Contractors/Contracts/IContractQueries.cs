using PDS.Portfolio.Contracts;

namespace PDS.Contractors.Contracts;

/// <summary>The only way other modules may read contract data.</summary>
public interface IContractQueries
{
    /// <summary>All contracts on the project (any status), with contractor names. Empty for an unknown project.</summary>
    Task<IReadOnlyList<ContractSummary>> GetProjectContracts(ProjectId projectId, CancellationToken ct);
}
