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

internal static class CloseRisk
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{riskId:guid}/close", Close)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<CloseRiskRequest>()
            .WithName("CloseRisk");
        group.MapPost("/{riskId:guid}/reopen", Reopen)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<ReopenRiskRequest>()
            .WithName("ReopenRisk");
    }

    internal static Task<Results<Ok<RiskDetails>, NotFound, ProblemHttpResult>> Close(
        Guid projectId, Guid riskId, CloseRiskRequest request, RiskStore store, ICurrentUser user,
        TimeProvider clock, CancellationToken ct) =>
        Change(
            projectId, riskId, request.Version, store,
            isNoOp: risk => risk.Status == RiskStatus.Closed,
            apply: (risk, stamp) => risk.Close(request.Note, stamp),
            user, clock, ct);

    internal static Task<Results<Ok<RiskDetails>, NotFound, ProblemHttpResult>> Reopen(
        Guid projectId, Guid riskId, ReopenRiskRequest request, RiskStore store, ICurrentUser user,
        TimeProvider clock, CancellationToken ct) =>
        Change(
            projectId, riskId, request.Version, store,
            isNoOp: risk => risk.Status != RiskStatus.Closed,
            apply: (risk, stamp) => risk.Reopen(stamp),
            user, clock, ct);

    private static async Task<Results<Ok<RiskDetails>, NotFound, ProblemHttpResult>> Change(
        Guid projectId, Guid riskId, long version, RiskStore store, Func<Risk, bool> isNoOp,
        Func<Risk, AuditStamp, bool> apply, ICurrentUser user, TimeProvider clock, CancellationToken ct)
    {
        var risk = await store.GetAsync(new ProjectId(projectId), new RiskId(riskId), ct);
        if (risk is null)
            return TypedResults.NotFound();
        if (isNoOp(risk))
            return TypedResults.Ok(RiskDetails.From(risk));
        if (risk.Version != version)
            return RisksProblems.VersionConflict();
        if (!apply(risk, new AuditStamp(clock.GetUtcNow(), user.Name)))
            return TypedResults.Ok(RiskDetails.From(risk));

        return await store.UpdateAsync(risk, version, ct)
            ? TypedResults.Ok(RiskDetails.From(risk))
            : RisksProblems.VersionConflict();
    }
}
