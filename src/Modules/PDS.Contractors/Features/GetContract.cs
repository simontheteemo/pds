using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using PDS.Contractors.Contracts;
using PDS.Contractors.Data;
using PDS.Portfolio.Contracts;

namespace PDS.Contractors.Features;

internal static class GetContract
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{contractId:guid}", Handle).WithName("GetContract");

    internal static async Task<Results<Ok<ContractDetails>, NotFound>> Handle(
        Guid projectId, Guid contractId, ContractStore store, ContractorStore contractors, CancellationToken ct)
    {
        var contract = await store.GetAsync(new ProjectId(projectId), new ContractId(contractId), ct);
        if (contract is null)
            return TypedResults.NotFound();

        var contractor = await contractors.GetAsync(contract.ContractorId, ct);
        return TypedResults.Ok(ContractDetails.From(contract, contractor?.Fields.Name ?? ContractListing.UnknownContractor));
    }
}
