using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using PDS.Costs.Contracts;
using PDS.Costs.Data;
using PDS.Portfolio.Contracts;
using PDS.Shared.Security;

namespace PDS.Costs.Features;

internal static class DeleteBudgetLine
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapDelete("/lines/{lineId:guid}", Handle)
            .RequireAuthorization(Policies.CanAdminister)
            .WithName("DeleteBudgetLine");

    internal static async Task<Results<NoContent, NotFound, ValidationProblem, ProblemHttpResult>> Handle(
        Guid projectId, Guid lineId, long? version, CostStore store, IPortfolioQueries portfolio, CancellationToken ct)
    {
        if (version is null or <= 0)
            return CostsProblems.InvalidVersion();

        var project = await ProjectLookup.FindAsync(portfolio, projectId, ct);
        if (project is null)
            return TypedResults.NotFound();
        if (project.IsArchived)
            return CostsProblems.ProjectArchived();

        var pid = new ProjectId(projectId);
        var id = new BudgetLineId(lineId);
        var line = await store.GetLineAsync(pid, id, ct);
        if (line is null)
            return TypedResults.NotFound();

        if (line.Version != version.Value || !await store.DeleteLineAsync(pid, id, version.Value, ct))
            return CostsProblems.VersionConflict("budget line");
        return TypedResults.NoContent();
    }
}
