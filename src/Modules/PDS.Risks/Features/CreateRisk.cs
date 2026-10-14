using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using PDS.Portfolio.Contracts;
using PDS.Risks.Data;
using PDS.Risks.Domain;
using PDS.Shared.Security;
using PDS.Shared.Web;

namespace PDS.Risks.Features;

internal static class CreateRisk
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<CreateRiskRequest>()
            .WithName("CreateRisk");

    internal static async Task<Results<Created<RiskDetails>, NotFound, ValidationProblem>> Handle(
        Guid projectId, CreateRiskRequest request, RiskStore store, IPortfolioQueries portfolio,
        ICurrentUser user, TimeProvider clock, CancellationToken ct)
    {
        var project = await ProjectLookup.FindAsync(portfolio, projectId, ct);
        if (project is null)
            return TypedResults.NotFound();
        if (project.IsArchived)
            return RisksProblems.ProjectArchived();

        var risk = Risk.Create(new ProjectId(projectId), request.ToFields(), new AuditStamp(clock.GetUtcNow(), user.Name));
        await store.CreateAsync(risk, ct);
        return TypedResults.Created($"/api/risks/projects/{projectId}/risks/{risk.Id.Value}", RiskDetails.From(risk));
    }
}
