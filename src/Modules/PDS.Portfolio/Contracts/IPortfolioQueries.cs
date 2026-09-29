namespace PDS.Portfolio.Contracts;

/// <summary>The only way other modules may read portfolio data.</summary>
public interface IPortfolioQueries
{
    Task<bool> Exists(ProjectId id, CancellationToken ct);

    /// <summary>Summaries for the known ids; unknown ids are skipped and duplicates collapse.</summary>
    Task<IReadOnlyList<ProjectSummary>> GetSummaries(IReadOnlyCollection<ProjectId> ids, CancellationToken ct);
}
