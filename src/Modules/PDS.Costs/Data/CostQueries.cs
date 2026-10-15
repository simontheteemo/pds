using PDS.Costs.Contracts;
using PDS.Costs.Domain;
using PDS.Portfolio.Contracts;

namespace PDS.Costs.Data;

internal sealed class CostQueries(CostStore store) : ICostQueries
{
    public async Task<ProjectCostPosition> GetProjectCostPosition(ProjectId projectId, CancellationToken ct)
    {
        var data = await store.LoadProjectAsync(projectId, ct);
        var (_, total) = CostCalculator.For(data.Lines, data.Commitments, data.Actuals);
        // Match the report: an actual on a deleted line counts toward no line, so it is left out here too.
        var lineIds = data.Lines.Select(l => l.Id).ToHashSet();
        var actuals = data.Actuals
            .Where(a => lineIds.Contains(a.Fields.BudgetLineId))
            .Select(a => new CostActualEntry(a.Fields.Date, a.Fields.Amount.Amount))
            .ToList();
        return new ProjectCostPosition(total.ForecastFinalCost, total.Actual, total.ForecastToComplete, actuals);
    }
}
