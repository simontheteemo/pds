using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using PDS.Costs.Contracts;
using PDS.Costs.Data;
using PDS.Portfolio.Contracts;
using PDS.Shared.Security;

namespace PDS.Costs.Features;

internal static class DeleteActualCost
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapDelete("/actuals/{entryId:guid}", Handle)
            .RequireAuthorization(Policies.CanAdminister)
            .WithName("DeleteActualCost");

    internal static async Task<Results<NoContent, NotFound, ValidationProblem, ProblemHttpResult>> Handle(
        Guid projectId, Guid entryId, long? version, CostStore store, IPortfolioQueries portfolio, CancellationToken ct)
    {
        if (version is null or <= 0)
            return CostsProblems.InvalidVersion();

        var project = await ProjectLookup.FindAsync(portfolio, projectId, ct);
        if (project is null)
            return TypedResults.NotFound();
        if (project.IsArchived)
            return CostsProblems.ProjectArchived();

        var pid = new ProjectId(projectId);
        var id = new ActualCostId(entryId);
        var entry = await store.GetActualAsync(pid, id, ct);
        if (entry is null)
            return TypedResults.NotFound();

        if (entry.Version != version.Value || !await store.DeleteActualAsync(pid, id, version.Value, ct))
            return CostsProblems.VersionConflict("actual cost");
        return TypedResults.NoContent();
    }
}
