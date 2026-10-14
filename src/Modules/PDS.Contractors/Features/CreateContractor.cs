using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Contractors.Data;
using PDS.Contractors.Domain;
using PDS.Shared.Security;
using PDS.Shared.Settings;
using PDS.Shared.Web;

namespace PDS.Contractors.Features;

internal static class CreateContractor
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<CreateContractorRequest>()
            .WithName("CreateContractor");

    internal static async Task<Results<Created<ContractorDetails>, ValidationProblem>> Handle(
        CreateContractorRequest request, ContractorStore store, ICurrentUser user, TimeProvider clock,
        IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var contractor = Contractor.Create(request.ToFields(), new AuditStamp(clock.GetUtcNow(), user.Name));
        await store.CreateAsync(contractor, ct);
        return TypedResults.Created(
            $"/api/contractors/{contractor.Id.Value}",
            ContractorDetails.From(contractor, LocalDate.Today(clock, locale.Value)));
    }
}
