using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Contractors.Contracts;
using PDS.Contractors.Data;
using PDS.Contractors.Domain;
using PDS.Shared.Security;
using PDS.Shared.Settings;
using PDS.Shared.Web;

namespace PDS.Contractors.Features;

internal static class SetContractorActive
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{contractorId:guid}/deactivate", Deactivate)
            .RequireAuthorization(Policies.CanAdminister)
            .WithValidation<SetActiveRequest>()
            .WithName("DeactivateContractor");
        group.MapPost("/{contractorId:guid}/activate", Activate)
            .RequireAuthorization(Policies.CanAdminister)
            .WithValidation<SetActiveRequest>()
            .WithName("ActivateContractor");
    }

    internal static Task<Results<Ok<ContractorDetails>, NotFound, ProblemHttpResult>> Deactivate(
        Guid contractorId, SetActiveRequest request, ContractorStore store, ICurrentUser user, TimeProvider clock,
        IOptions<LocaleOptions> locale, CancellationToken ct) =>
        Change(contractorId, request.Version, false, store, user, clock, locale.Value, ct);

    internal static Task<Results<Ok<ContractorDetails>, NotFound, ProblemHttpResult>> Activate(
        Guid contractorId, SetActiveRequest request, ContractorStore store, ICurrentUser user, TimeProvider clock,
        IOptions<LocaleOptions> locale, CancellationToken ct) =>
        Change(contractorId, request.Version, true, store, user, clock, locale.Value, ct);

    private static async Task<Results<Ok<ContractorDetails>, NotFound, ProblemHttpResult>> Change(
        Guid contractorId, long version, bool active, ContractorStore store, ICurrentUser user, TimeProvider clock,
        LocaleOptions locale, CancellationToken ct)
    {
        var today = LocalDate.Today(clock, locale);
        var contractor = await store.GetAsync(new ContractorId(contractorId), ct);
        if (contractor is null)
            return TypedResults.NotFound();
        if (contractor.IsActive == active)
            return TypedResults.Ok(ContractorDetails.From(contractor, today));
        if (contractor.Version != version)
            return ContractorsProblems.VersionConflict("contractor");
        if (!contractor.SetActive(active, new AuditStamp(clock.GetUtcNow(), user.Name)))
            return TypedResults.Ok(ContractorDetails.From(contractor, today));

        return await store.UpdateAsync(contractor, version, ct)
            ? TypedResults.Ok(ContractorDetails.From(contractor, today))
            : ContractorsProblems.VersionConflict("contractor");
    }
}
