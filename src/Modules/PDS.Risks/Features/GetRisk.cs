using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using PDS.Portfolio.Contracts;
using PDS.Risks.Contracts;
using PDS.Risks.Data;

namespace PDS.Risks.Features;

internal static class GetRisk
{
    public static void Map(RouteGroupBuilder group) => group.MapGet("/{riskId:guid}", Handle).WithName("GetRisk");

    internal static async Task<Results<Ok<RiskDetails>, NotFound>> Handle(
        Guid projectId, Guid riskId, RiskStore store, CancellationToken ct)
    {
        var risk = await store.GetAsync(new ProjectId(projectId), new RiskId(riskId), ct);
        return risk is null ? TypedResults.NotFound() : TypedResults.Ok(RiskDetails.From(risk));
    }
}
