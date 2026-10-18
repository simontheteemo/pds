namespace PDS.Portfolio.Contracts;

/// <summary>The only way other modules may read portfolio data.</summary>
public interface IPortfolioQueries
{
    Task<bool> Exists(ProjectId id, CancellationToken ct);

    /// <summary>Summaries for the known ids; unknown ids are skipped and duplicates collapse.</summary>
    Task<IReadOnlyList<ProjectSummary>> GetSummaries(IReadOnlyCollection<ProjectId> ids, CancellationToken ct);

    /// <summary>The project's dates, or null when the project does not exist.</summary>
    Task<ProjectSchedule?> GetSchedule(ProjectId id, CancellationToken ct);

    /// <summary>The project's milestones in programme order; empty when none or the project doesn't exist.</summary>
    Task<IReadOnlyList<MilestoneSummary>> GetMilestones(ProjectId id, CancellationToken ct);

    /// <summary>Every incomplete milestone on non-archived projects, ordered by due date (forecast, else planned; undated last).</summary>
    Task<IReadOnlyList<MilestoneSummary>> GetOpenMilestones(CancellationToken ct);
}
