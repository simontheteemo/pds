using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Portfolio.Contracts;
using PDS.Sales.Contracts;
using PDS.Sales.Data;
using PDS.Sales.Domain;
using PDS.Shared.Security;
using PDS.Shared.Settings;
using PDS.Shared.Web;

namespace PDS.Sales.Features;

internal static class UpdateUnit
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPut("/{unitId:guid}", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<UpdateUnitRequest>()
            .WithName("UpdateUnit");

    internal static async Task<Results<Ok<UnitDetails>, NotFound, ValidationProblem, ProblemHttpResult>> Handle(
        Guid projectId, Guid unitId, UpdateUnitRequest request, UnitStore store, ICurrentUser user,
        TimeProvider clock, IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var project = new ProjectId(projectId);
        var unit = await store.GetAsync(project, new UnitId(unitId), ct);
        if (unit is null)
            return TypedResults.NotFound();
        if (unit.Version != request.Version)
            return SalesProblems.VersionConflict();

        var units = await store.ListForProjectAsync(project, ct);
        if (UnitIdentifiers.IsTaken(units, request.Identifier, unit.Id))
            return SalesProblems.IdentifierTaken();

        var currency = locale.Value.Currency;
        unit.Update(request.ToFields(currency), new AuditStamp(clock.GetUtcNow(), user.Name));
        return await store.UpdateAsync(unit, request.Version, ct)
            ? TypedResults.Ok(UnitDetails.From(unit, currency))
            : SalesProblems.VersionConflict();
    }
}
