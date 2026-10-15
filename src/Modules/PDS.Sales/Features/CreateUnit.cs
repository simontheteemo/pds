using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Portfolio.Contracts;
using PDS.Sales.Data;
using PDS.Sales.Domain;
using PDS.Shared.Security;
using PDS.Shared.Settings;
using PDS.Shared.Web;

namespace PDS.Sales.Features;

internal static class CreateUnit
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<CreateUnitRequest>()
            .WithName("CreateUnit");

    internal static async Task<Results<Created<UnitDetails>, NotFound, ValidationProblem>> Handle(
        Guid projectId, CreateUnitRequest request, UnitStore store, IPortfolioQueries portfolio,
        ICurrentUser user, TimeProvider clock, IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var project = await ProjectLookup.FindAsync(portfolio, projectId, ct);
        if (project is null)
            return TypedResults.NotFound();
        if (project.IsArchived)
            return SalesProblems.ProjectArchived();

        var id = new ProjectId(projectId);
        var existing = await store.ListForProjectAsync(id, ct);
        if (UnitIdentifiers.IsTaken(existing, request.Identifier, null))
            return SalesProblems.IdentifierTaken();

        var currency = locale.Value.Currency;
        var unit = Unit.Create(id, request.ToFields(currency), new AuditStamp(clock.GetUtcNow(), user.Name));
        await store.CreateAsync(unit, ct);
        return TypedResults.Created(
            $"/api/sales/projects/{projectId}/units/{unit.Id.Value}", UnitDetails.From(unit, currency));
    }
}
