using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Portfolio.Contracts;
using PDS.Portfolio.Data;
using PDS.Portfolio.Domain;
using PDS.Shared.Security;
using PDS.Shared.Settings;
using PDS.Shared.Web;

namespace PDS.Portfolio.Features;

internal static class UpdateMilestone
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPut("/projects/{id:guid}/milestones/{milestoneId:guid}", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<UpdateMilestoneRequest>()
            .WithName("UpdateMilestone");

    internal static async Task<Results<Ok<MilestoneDetails>, NotFound, ValidationProblem, ProblemHttpResult>> Handle(
        Guid id, Guid milestoneId, UpdateMilestoneRequest request, ProjectStore projects, MilestoneStore store,
        ICurrentUser user, TimeProvider clock, IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var pid = new ProjectId(id);
        var project = await projects.GetAsync(pid, ct);
        if (project is null)
            return TypedResults.NotFound();
        if (project.IsArchived)
            return MilestoneProblems.ProjectArchived();

        var milestone = await store.GetAsync(pid, new MilestoneId(milestoneId), ct);
        if (milestone is null)
            return TypedResults.NotFound();

        var key = MilestoneRules.NameKey(request.Name!);
        var all = await store.ListForProjectAsync(pid, ct);
        if (all.Any(m => m.Id != milestone.Id && MilestoneRules.NameKey(m.Fields.Name) == key))
            return MilestoneProblems.DuplicateName();
        if (milestone.Version != request.Version)
            return MilestoneProblems.VersionConflict();

        milestone.Update(request.ToFields(request.SortOrder!.Value), new AuditStamp(clock.GetUtcNow(), user.Name));
        return await store.UpdateAsync(milestone, request.Version, ct)
            ? TypedResults.Ok(MilestoneDetails.From(milestone, LocalDate.Today(clock, locale.Value)))
            : MilestoneProblems.VersionConflict();
    }
}
