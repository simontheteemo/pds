using PDS.Portfolio.Contracts;

namespace PDS.Costs.Contracts;

public interface ICostQueries
{
    /// <summary>Zeros and no actuals for a project with no cost data (or an unknown project).</summary>
    Task<ProjectCostPosition> GetProjectCostPosition(ProjectId projectId, CancellationToken ct);
}
