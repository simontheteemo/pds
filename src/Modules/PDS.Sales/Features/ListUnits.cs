using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Portfolio.Contracts;
using PDS.Sales.Data;
using PDS.Sales.Domain;
using PDS.Shared.Settings;

namespace PDS.Sales.Features;

internal static class ListUnits
{
    public static void Map(RouteGroupBuilder group) => group.MapGet("", Handle).WithName("ListUnits");

    internal static async Task<Results<Ok<ProjectSales>, NotFound>> Handle(
        Guid projectId, UnitStore store, IPortfolioQueries portfolio, IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        if (await ProjectLookup.FindAsync(portfolio, projectId, ct) is null)
            return TypedResults.NotFound();

        var currency = locale.Value.Currency;
        var units = await store.ListForProjectAsync(new ProjectId(projectId), ct);
        var ordered = units
            .OrderBy(u => u.Fields.Identifier, UnitIdentifiers.Order)
            .Select(u => UnitDetails.From(u, currency))
            .ToList();
        return TypedResults.Ok(new ProjectSales(SalesSummary.From(SalesTotals.For(units), currency), ordered));
    }
}
