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

internal static class ListSalesSummaries
{
    public static void Map(RouteGroupBuilder group) => group.MapGet("/summary", Handle).WithName("ListSalesSummaries");

    internal static async Task<Ok<List<ProjectSalesSummary>>> Handle(
        UnitStore store, IPortfolioQueries portfolio, IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var currency = locale.Value.Currency;
        var groups = (await store.ListAllAsync(ct)).GroupBy(u => u.ProjectId).ToDictionary(g => g.Key, g => g.ToList());
        var projects = await portfolio.GetSummaries(groups.Keys.ToList(), ct);

        var result = projects
            .Where(p => groups.ContainsKey(p.Id))
            .OrderBy(p => p.Code, StringComparer.OrdinalIgnoreCase)
            .Select(p => new ProjectSalesSummary(
                p.Id.Value, p.Code, p.Name, p.IsArchived, SalesSummary.From(SalesTotals.For(groups[p.Id]), currency)))
            .ToList();
        return TypedResults.Ok(result);
    }
}
