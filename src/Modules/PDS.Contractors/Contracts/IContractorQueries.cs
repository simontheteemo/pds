namespace PDS.Contractors.Contracts;

/// <summary>The only way other modules may read contractor data.</summary>
public interface IContractorQueries
{
    /// <summary>Summaries for the ids that exist; unknown ids are omitted.</summary>
    Task<IReadOnlyList<ContractorSummary>> GetContractors(IReadOnlyCollection<ContractorId> ids, CancellationToken ct);
}
