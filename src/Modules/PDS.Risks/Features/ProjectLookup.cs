using PDS.Portfolio.Contracts;

namespace PDS.Risks.Features;

internal static class ProjectLookup
{
    /// <summary>The project's summary, or null when it does not exist.</summary>
    public static async Task<ProjectSummary?> FindAsync(IPortfolioQueries portfolio, Guid projectId, CancellationToken ct) =>
        (await portfolio.GetSummaries([new ProjectId(projectId)], ct)).FirstOrDefault();
}
