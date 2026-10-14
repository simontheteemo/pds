using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Portfolio.Contracts;
using PDS.Sales.Contracts;
using PDS.Sales.Data;
using PDS.Shared.Settings;

namespace PDS.Sales.Features;

internal static class GetUnit
{
    public static void Map(RouteGroupBuilder group) => group.MapGet("/{unitId:guid}", Handle).WithName("GetUnit");

    internal static async Task<Results<Ok<UnitDetails>, NotFound>> Handle(
        Guid projectId, Guid unitId, UnitStore store, IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var unit = await store.GetAsync(new ProjectId(projectId), new UnitId(unitId), ct);
        return unit is null ? TypedResults.NotFound() : TypedResults.Ok(UnitDetails.From(unit, locale.Value.Currency));
    }
}
