using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Actions.Data;
using PDS.Portfolio.Contracts;
using PDS.Shared.Settings;

namespace PDS.Actions.Features;

internal static class ListOverdueActions
{
    public static void Map(RouteGroupBuilder group) => group.MapGet("/overdue", Handle).WithName("ListOverdueActions");

    /// <summary>Every overdue open action across active projects, oldest due date first.</summary>
    internal static async Task<Ok<List<ActionSummary>>> Handle(
        ActionStore store, IPortfolioQueries portfolio, TimeProvider clock, IOptions<LocaleOptions> locale,
        CancellationToken ct)
    {
        var today = LocalDate.Today(clock, locale.Value);
        var actions = (await store.ListOverdueAsync(today, ct)).Where(a => a.IsOverdue(today));
        var joined = await ActionSummary.JoinProjects(portfolio, actions, today, ct);
        var ordered = joined
            .OrderBy(a => a.DueDate ?? DateOnly.MaxValue)
            .ThenBy(a => a.ProjectCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return TypedResults.Ok(ordered);
    }
}
