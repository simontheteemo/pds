using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Contractors.Contracts;
using PDS.Contractors.Data;
using PDS.Shared.Settings;

namespace PDS.Contractors.Features;

internal static class GetContractor
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{contractorId:guid}", Handle).WithName("GetContractor");

    internal static async Task<Results<Ok<ContractorDetails>, NotFound>> Handle(
        Guid contractorId, ContractorStore store, TimeProvider clock, IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var contractor = await store.GetAsync(new ContractorId(contractorId), ct);
        return contractor is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(ContractorDetails.From(contractor, LocalDate.Today(clock, locale.Value)));
    }
}
