using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using PDS.Consents.Domain;

namespace PDS.Consents.Features;

internal static class ListDue
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/due", Handle).WithName("ListDueConsentItems");

    internal static async Task<Ok<List<DueItem>>> Handle(DueItemsQuery query, CancellationToken ct) =>
        TypedResults.Ok((await query.LoadAsync(ct)).Items);
}
