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

internal static class UpdateContractor
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPut("/{contractorId:guid}", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<UpdateContractorRequest>()
            .WithName("UpdateContractor");

    internal static async Task<Results<Ok<ContractorDetails>, NotFound, ValidationProblem, ProblemHttpResult>> Handle(
        Guid contractorId, UpdateContractorRequest request, ContractorStore store, ICurrentUser user,
        TimeProvider clock, IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var contractor = await store.GetAsync(new ContractorId(contractorId), ct);
        if (contractor is null)
            return TypedResults.NotFound();
        if (contractor.Version != request.Version)
            return ContractorsProblems.VersionConflict("contractor");

        contractor.Update(request.ToFields(), new AuditStamp(clock.GetUtcNow(), user.Name));
        return await store.UpdateAsync(contractor, request.Version, ct)
            ? TypedResults.Ok(ContractorDetails.From(contractor, LocalDate.Today(clock, locale.Value)))
            : ContractorsProblems.VersionConflict("contractor");
    }
}
