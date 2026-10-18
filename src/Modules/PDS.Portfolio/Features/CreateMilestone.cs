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

internal static class CreateMilestone
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/projects/{id:guid}/milestones", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<CreateMilestoneRequest>()
            .WithName("CreateMilestone");

    internal static async Task<Results<Created<MilestoneDetails>, NotFound, ValidationProblem>> Handle(
        Guid id, CreateMilestoneRequest request, ProjectStore projects, MilestoneStore store, ICurrentUser user,
        TimeProvider clock, IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var pid = new ProjectId(id);
        var project = await projects.GetAsync(pid, ct);
        if (project is null)
            return TypedResults.NotFound();
        if (project.IsArchived)
            return MilestoneProblems.ProjectArchived();

        var existing = await store.ListForProjectAsync(pid, ct);
        var key = MilestoneRules.NameKey(request.Name!);
        if (existing.Any(m => MilestoneRules.NameKey(m.Fields.Name) == key))
            return MilestoneProblems.DuplicateName();

        var defaultOrder = existing.Count == 0
            ? 10
            : Math.Min((existing.Max(m => m.Fields.SortOrder) / 10 + 1) * 10, MilestoneRules.MaxSortOrder);
        var milestone = Milestone.Create(pid, request.ToFields(defaultOrder), new AuditStamp(clock.GetUtcNow(), user.Name));
        await store.CreateAsync(milestone, ct);
        return TypedResults.Created(
            $"/api/portfolio/projects/{id}/milestones", MilestoneDetails.From(milestone, LocalDate.Today(clock, locale.Value)));
    }
}
