using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using PDS.Portfolio.Contracts;
using PDS.Risks.Contracts;
using PDS.Risks.Data;
using PDS.Risks.Domain;
using PDS.Shared.Security;
using PDS.Shared.Web;

namespace PDS.Risks.Features;

internal static class UpdateRisk
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPut("/{riskId:guid}", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<UpdateRiskRequest>()
            .WithName("UpdateRisk");

    internal static async Task<Results<Ok<RiskDetails>, NotFound, ValidationProblem, ProblemHttpResult>> Handle(
        Guid projectId, Guid riskId, UpdateRiskRequest request, RiskStore store, ICurrentUser user,
        TimeProvider clock, CancellationToken ct)
    {
        var risk = await store.GetAsync(new ProjectId(projectId), new RiskId(riskId), ct);
        if (risk is null)
            return TypedResults.NotFound();
        if (risk.Version != request.Version)
            return RisksProblems.VersionConflict();
        if (risk.Status == RiskStatus.Closed)
            return RisksProblems.RiskClosed();

        risk.Update(request.ToFields(), request.Status!.Value, new AuditStamp(clock.GetUtcNow(), user.Name));
        return await store.UpdateAsync(risk, request.Version, ct)
            ? TypedResults.Ok(RiskDetails.From(risk))
            : RisksProblems.VersionConflict();
    }
}
