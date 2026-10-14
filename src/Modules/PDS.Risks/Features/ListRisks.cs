using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using PDS.Portfolio.Contracts;
using PDS.Risks.Data;
using PDS.Risks.Domain;

namespace PDS.Risks.Features;

internal static class ListRisks
{
    public static void Map(RouteGroupBuilder group) => group.MapGet("", Handle).WithName("ListRisks");

    internal static async Task<Results<Ok<List<RiskDetails>>, NotFound>> Handle(
        Guid projectId, bool? includeClosed, RiskStore store, IPortfolioQueries portfolio, CancellationToken ct)
    {
        if (await ProjectLookup.FindAsync(portfolio, projectId, ct) is null)
            return TypedResults.NotFound();

        var risks = await store.ListForProjectAsync(new ProjectId(projectId), ct);
        var ordered = risks
            .Where(r => includeClosed == true || r.Status != RiskStatus.Closed)
            .OrderBy(r => r.Status == RiskStatus.Closed)
            .ThenByDescending(r => r.Score)
            .ThenBy(r => r.Fields.Title, StringComparer.OrdinalIgnoreCase)
            .Select(RiskDetails.From)
            .ToList();
        return TypedResults.Ok(ordered);
    }
}
