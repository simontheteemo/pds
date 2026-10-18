using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Contractors.Contracts;
using PDS.Costs.Contracts;
using PDS.Costs.Data;
using PDS.Costs.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared.Security;
using PDS.Shared.Settings;
using PDS.Shared.Web;

namespace PDS.Costs.Features;

internal static class UpdateActualCost
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPut("/actuals/{entryId:guid}", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<UpdateActualCostRequest>()
            .WithName("UpdateActualCost");

    internal static async Task<Results<Ok<ActualCostDetails>, NotFound, ValidationProblem, ProblemHttpResult>> Handle(
        Guid projectId, Guid entryId, UpdateActualCostRequest request, CostStore store, IPortfolioQueries portfolio,
        IContractQueries contracts, IContractorQueries contractors, ICurrentUser user, TimeProvider clock,
        IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var project = await ProjectLookup.FindAsync(portfolio, projectId, ct);
        if (project is null)
            return TypedResults.NotFound();
        if (project.IsArchived)
            return CostsProblems.ProjectArchived();

        var pid = new ProjectId(projectId);
        var entry = await store.GetActualAsync(pid, new ActualCostId(entryId), ct);
        if (entry is null)
            return TypedResults.NotFound();

        var data = await store.LoadProjectAsync(pid, ct);
        if (await CostReferences.CheckActualAsync(
                pid, data, request.BudgetLineId!.Value, request.ContractId, request.ContractorId, entry.Fields.ContractorId, entry.Id.Value,
                contracts, contractors, store, request.Reference, ct) is { } problem)
            return problem;
        if (entry.Version != request.Version)
            return CostsProblems.VersionConflict("actual cost");

        entry.Update(request.ToFields(locale.Value.Currency), new AuditStamp(clock.GetUtcNow(), user.Name));
        return await store.UpdateActualAsync(entry, request.Version, ct)
            ? TypedResults.Ok(ActualCostDetails.From(entry))
            : CostsProblems.VersionConflict("actual cost");
    }
}
