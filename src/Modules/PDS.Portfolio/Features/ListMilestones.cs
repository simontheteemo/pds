using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Portfolio.Contracts;
using PDS.Portfolio.Data;
using PDS.Portfolio.Domain;
using PDS.Shared.Settings;

namespace PDS.Portfolio.Features;

internal static class ListMilestones
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/projects/{id:guid}/milestones", Handle).WithName("ListMilestones");

    internal static async Task<Results<Ok<MilestoneList>, NotFound>> Handle(
        Guid id, ProjectStore projects, MilestoneStore store, TimeProvider clock, IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var pid = new ProjectId(id);
        if (await projects.GetAsync(pid, ct) is null)
            return TypedResults.NotFound();

        var today = LocalDate.Today(clock, locale.Value);
        var milestones = (await store.ListForProjectAsync(pid, ct)).Order(MilestoneRules.ProjectOrder);
        return TypedResults.Ok(new MilestoneList(today, milestones.Select(m => MilestoneDetails.From(m, today)).ToList()));
    }
}
