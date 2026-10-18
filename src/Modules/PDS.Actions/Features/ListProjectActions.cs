using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Actions.Data;
using PDS.Portfolio.Contracts;
using PDS.Shared.Settings;

namespace PDS.Actions.Features;

internal static class ListProjectActions
{
    public static void Map(RouteGroupBuilder group) => group.MapGet("", Handle).WithName("ListProjectActions");

    /// <summary>
    /// Open actions first: overdue, then by due date (none last), priority (high first) and title.
    /// Closed actions (when included) follow, most recently closed first.
    /// </summary>
    internal static async Task<Results<Ok<List<ActionDetails>>, NotFound>> Handle(
        Guid projectId, bool? includeClosed, ActionStore store, IPortfolioQueries portfolio, TimeProvider clock,
        IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        if (await ProjectLookup.FindAsync(portfolio, projectId, ct) is null)
            return TypedResults.NotFound();

        var today = LocalDate.Today(clock, locale.Value);
        var actions = await store.ListForProjectAsync(new ProjectId(projectId), ct);
        var ordered = actions
            .Where(a => includeClosed == true || a.IsOpen)
            .OrderBy(a => !a.IsOpen)
            .ThenByDescending(a => a.IsOpen ? DateTimeOffset.MinValue : a.Completed?.At ?? DateTimeOffset.MinValue)
            .ThenByDescending(a => a.IsOverdue(today))
            .ThenBy(a => a.Fields.DueDate ?? DateOnly.MaxValue)
            .ThenByDescending(a => a.Fields.Priority)
            .ThenBy(a => a.Fields.Title, StringComparer.OrdinalIgnoreCase)
            .Select(a => ActionDetails.From(a, today))
            .ToList();
        return TypedResults.Ok(ordered);
    }
}
