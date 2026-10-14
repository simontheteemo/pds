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

internal static class CreateContract
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<CreateContractRequest>()
            .WithName("CreateContract");

    internal static async Task<Results<Created<ContractDetails>, NotFound, ValidationProblem>> Handle(
        Guid projectId, CreateContractRequest request, ContractStore store, ContractorStore contractors,
        IPortfolioQueries portfolio, ICurrentUser user, TimeProvider clock, IOptions<LocaleOptions> locale,
        CancellationToken ct)
    {
        var project = await ProjectLookup.FindAsync(portfolio, projectId, ct);
        if (project is null)
            return TypedResults.NotFound();
        if (project.IsArchived)
            return ContractorsProblems.ProjectArchived();

        var contractor = await contractors.GetAsync(new ContractorId(request.ContractorId!.Value), ct);
        if (contractor is not { IsActive: true })
            return ContractorsProblems.ContractorUnavailable();

        var contract = Contract.Create(
            new ProjectId(projectId), contractor.Id, request.ToFields(locale.Value.Currency),
            new AuditStamp(clock.GetUtcNow(), user.Name));
        await store.CreateAsync(contract, ct);
        return TypedResults.Created(
            $"/api/contractors/projects/{projectId}/contracts/{contract.Id.Value}",
            ContractDetails.From(contract, contractor.Fields.Name));
    }
}
