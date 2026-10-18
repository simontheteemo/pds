using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using PDS.Portfolio.Contracts;
using PDS.Portfolio.Data;
using PDS.Shared.Security;

namespace PDS.Portfolio.Features;

internal static class DeleteMilestone
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapDelete("/projects/{id:guid}/milestones/{milestoneId:guid}", Handle)
            .RequireAuthorization(Policies.CanAdminister)
            .WithName("DeleteMilestone");

    internal static async Task<Results<NoContent, NotFound, ValidationProblem, ProblemHttpResult>> Handle(
        Guid id, Guid milestoneId, long? version, ProjectStore projects, MilestoneStore store, CancellationToken ct)
    {
        if (version is null or <= 0)
            return MilestoneProblems.InvalidVersion();

        var pid = new ProjectId(id);
        var project = await projects.GetAsync(pid, ct);
        if (project is null)
            return TypedResults.NotFound();
        if (project.IsArchived)
            return MilestoneProblems.ProjectArchived();

        var mid = new MilestoneId(milestoneId);
        var milestone = await store.GetAsync(pid, mid, ct);
        if (milestone is null)
            return TypedResults.NotFound();

        if (milestone.Version != version.Value || !await store.DeleteAsync(pid, mid, version.Value, ct))
            return MilestoneProblems.VersionConflict();
        return TypedResults.NoContent();
    }
}
