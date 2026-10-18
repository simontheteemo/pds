using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Contractors.Contracts;
using PDS.Costs.Data;
using PDS.Costs.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared.Security;
using PDS.Shared.Settings;
using PDS.Shared.Web;

namespace PDS.Costs.Features;

internal static class CreateActualCost
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/actuals", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<CreateActualCostRequest>()
            .WithName("CreateActualCost");

    internal static async Task<Results<Created<ActualCostDetails>, NotFound, ValidationProblem>> Handle(
        Guid projectId, CreateActualCostRequest request, CostStore store, IPortfolioQueries portfolio, IContractQueries contracts,
        IContractorQueries contractors, ICurrentUser user, TimeProvider clock, IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var project = await ProjectLookup.FindAsync(portfolio, projectId, ct);
        if (project is null)
            return TypedResults.NotFound();
        if (project.IsArchived)
            return CostsProblems.ProjectArchived();

        var pid = new ProjectId(projectId);
        var data = await store.LoadProjectAsync(pid, ct);
        if (await CostReferences.CheckActualAsync(
                pid, data, request.BudgetLineId!.Value, request.ContractId, request.ContractorId, null, null,
                contracts, contractors, store, request.Reference, ct) is { } problem)
            return problem;

        var entry = ActualCost.Create(pid, request.ToFields(locale.Value.Currency), new AuditStamp(clock.GetUtcNow(), user.Name));
        await store.CreateActualAsync(entry, ct);
        return TypedResults.Created($"/api/costs/projects/{projectId}", ActualCostDetails.From(entry));
    }
}
