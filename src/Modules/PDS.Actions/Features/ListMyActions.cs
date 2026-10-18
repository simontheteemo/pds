using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Actions.Data;
using PDS.Portfolio.Contracts;
using PDS.Shared.Security;
using PDS.Shared.Settings;

namespace PDS.Actions.Features;

internal static class ListMyActions
{
    public static void Map(RouteGroupBuilder group) => group.MapGet("/mine", Handle).WithName("ListMyActions");

    /// <summary>The signed-in user's open actions: overdue first, then due date (none last), priority (high first), title.</summary>
    internal static async Task<Ok<List<ActionSummary>>> Handle(
        ActionStore store, IPortfolioQueries portfolio, ICurrentUser user, TimeProvider clock,
        IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var today = LocalDate.Today(clock, locale.Value);
        var actions = (await store.ListOpenForOwnerAsync(user.Id, ct)).Where(a => a.IsOpen);
        var joined = await ActionSummary.JoinProjects(portfolio, actions, today, ct);
        var ordered = joined
            .OrderByDescending(a => a.IsOverdue)
            .ThenBy(a => a.DueDate ?? DateOnly.MaxValue)
            .ThenByDescending(a => a.Priority)
            .ThenBy(a => a.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return TypedResults.Ok(ordered);
    }
}
