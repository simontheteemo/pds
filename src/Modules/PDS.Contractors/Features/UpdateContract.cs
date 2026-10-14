using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Contractors.Contracts;
using PDS.Contractors.Data;
using PDS.Contractors.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared.Security;
using PDS.Shared.Settings;
using PDS.Shared.Web;

namespace PDS.Contractors.Features;

internal static class UpdateContract
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPut("/{contractId:guid}", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<UpdateContractRequest>()
            .WithName("UpdateContract");

    internal static async Task<Results<Ok<ContractDetails>, NotFound, ValidationProblem, ProblemHttpResult>> Handle(
        Guid projectId, Guid contractId, UpdateContractRequest request, ContractStore store,
        ContractorStore contractors, ICurrentUser user, TimeProvider clock, IOptions<LocaleOptions> locale,
        CancellationToken ct)
    {
        var contract = await store.GetAsync(new ProjectId(projectId), new ContractId(contractId), ct);
        if (contract is null)
            return TypedResults.NotFound();
        if (request.ContractorId != contract.ContractorId.Value)
            return ContractorsProblems.ContractorChanged();
        if (contract.Version != request.Version)
            return ContractorsProblems.VersionConflict("contract");

        contract.Update(request.ToFields(locale.Value.Currency), new AuditStamp(clock.GetUtcNow(), user.Name));
        if (!await store.UpdateAsync(contract, request.Version, ct))
            return ContractorsProblems.VersionConflict("contract");

        var contractor = await contractors.GetAsync(contract.ContractorId, ct);
        return TypedResults.Ok(ContractDetails.From(contract, contractor?.Fields.Name ?? ContractListing.UnknownContractor, locale.Value.Currency));
    }
}
