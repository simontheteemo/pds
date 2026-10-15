using PDS.Portfolio.Contracts;

namespace PDS.Documents.Features;

internal static class ProjectLookup
{
    public static async Task<ProjectSummary?> FindAsync(IPortfolioQueries portfolio, Guid projectId, CancellationToken ct) =>
        (await portfolio.GetSummaries([new ProjectId(projectId)], ct)).FirstOrDefault();
}
