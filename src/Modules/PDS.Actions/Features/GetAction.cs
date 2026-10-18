using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Actions.Contracts;
using PDS.Actions.Data;
using PDS.Portfolio.Contracts;
using PDS.Shared.Settings;

namespace PDS.Actions.Features;

internal static class GetAction
{
    public static void Map(RouteGroupBuilder group) => group.MapGet("/{actionId:guid}", Handle).WithName("GetAction");

    internal static async Task<Results<Ok<ActionDetails>, NotFound>> Handle(
        Guid projectId, Guid actionId, ActionStore store, TimeProvider clock, IOptions<LocaleOptions> locale,
        CancellationToken ct)
    {
        var action = await store.GetAsync(new ProjectId(projectId), new ActionId(actionId), ct);
        return action is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(ActionDetails.From(action, LocalDate.Today(clock, locale.Value)));
    }
}
