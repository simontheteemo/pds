using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Consents.Contracts;
using PDS.Consents.Data;
using PDS.Consents.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared.Security;
using PDS.Shared.Settings;
using PDS.Shared.Web;

namespace PDS.Consents.Features;

internal static class UpdateCondition
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPut("/{consentId:guid}/conditions/{conditionId:guid}", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<UpdateConditionRequest>()
            .WithName("UpdateCondition");

    internal static async Task<Results<Ok<ConditionDetails>, NotFound, ValidationProblem, ProblemHttpResult>> Handle(
        Guid projectId, Guid consentId, Guid conditionId, UpdateConditionRequest request, ConsentStore store,
        ICurrentUser user, TimeProvider clock, IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var record = await store.GetAsync(new ProjectId(projectId), new ConsentId(consentId), ct);
        if (record is null)
            return TypedResults.NotFound();
        var condition = await store.GetConditionAsync(
            new ProjectId(projectId), new ConsentId(consentId), new ConditionId(conditionId), ct);
        if (condition is null)
            return TypedResults.NotFound();
        if (condition.Version != request.Version)
            return ConsentsProblems.VersionConflict("condition");

        condition.Update(request.ToFields(), new AuditStamp(clock.GetUtcNow(), user.Name));
        if (!await store.UpdateConditionAsync(condition, request.Version, ct))
            return ConsentsProblems.VersionConflict("condition");
        return TypedResults.Ok(ConditionDetails.From(condition, record.Consent.IsActive, LocalDate.Today(clock, locale.Value)));
    }
}
