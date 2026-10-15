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

internal static class UpdateSale
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPut("/{unitId:guid}/sale", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<UpdateSaleRequest>()
            .WithName("UpdateSale");

    internal static async Task<Results<Ok<UnitDetails>, NotFound, ValidationProblem, ProblemHttpResult>> Handle(
        Guid projectId, Guid unitId, UpdateSaleRequest request, UnitStore store, ICurrentUser user,
        TimeProvider clock, IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var unit = await store.GetAsync(new ProjectId(projectId), new UnitId(unitId), ct);
        if (unit is null)
            return TypedResults.NotFound();
        if (unit.Version != request.Version)
            return SalesProblems.VersionConflict();

        var currency = locale.Value.Currency;
        var status = request.Status!.Value;
        unit.UpdateSale(status, SaleRules.HasSale(status) ? request.ToSale(currency) : null, new AuditStamp(clock.GetUtcNow(), user.Name));
        return await store.UpdateAsync(unit, request.Version, ct)
            ? TypedResults.Ok(UnitDetails.From(unit, currency))
            : SalesProblems.VersionConflict();
    }
}
