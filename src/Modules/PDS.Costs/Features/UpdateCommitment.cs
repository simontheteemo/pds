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

internal static class UpdateCommitment
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPut("/commitments/{entryId:guid}", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<UpdateCommitmentRequest>()
            .WithName("UpdateCommitment");

    internal static async Task<Results<Ok<CommitmentDetails>, NotFound, ValidationProblem, ProblemHttpResult>> Handle(
        Guid projectId, Guid entryId, UpdateCommitmentRequest request, CostStore store, IPortfolioQueries portfolio,
        IContractQueries contracts, ICurrentUser user, TimeProvider clock, IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var project = await ProjectLookup.FindAsync(portfolio, projectId, ct);
        if (project is null)
            return TypedResults.NotFound();
        if (project.IsArchived)
            return CostsProblems.ProjectArchived();

        var pid = new ProjectId(projectId);
        var entry = await store.GetCommitmentAsync(pid, new CommitmentId(entryId), ct);
        if (entry is null)
            return TypedResults.NotFound();

        var data = await store.LoadProjectAsync(pid, ct);
        if (await CostReferences.CheckAsync(pid, data, request.BudgetLineId!.Value, request.ContractId, contracts, ct) is { } problem)
            return problem;
        if (entry.Version != request.Version)
            return CostsProblems.VersionConflict("commitment");

        entry.Update(request.ToFields(locale.Value.Currency), new AuditStamp(clock.GetUtcNow(), user.Name));
        return await store.UpdateCommitmentAsync(entry, request.Version, ct)
            ? TypedResults.Ok(CommitmentDetails.From(entry))
            : CostsProblems.VersionConflict("commitment");
    }
}
