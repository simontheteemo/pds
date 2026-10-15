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

internal static class UpdateConsent
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPut("/{consentId:guid}", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<UpdateConsentRequest>()
            .WithName("UpdateConsent");

    internal static async Task<Results<Ok<ConsentDetails>, NotFound, ValidationProblem, ProblemHttpResult>> Handle(
        Guid projectId, Guid consentId, UpdateConsentRequest request, ConsentStore store, ICurrentUser user,
        TimeProvider clock, IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var record = await store.GetAsync(new ProjectId(projectId), new ConsentId(consentId), ct);
        if (record is null)
            return TypedResults.NotFound();
        var consent = record.Consent;
        if (consent.Version != request.Version)
            return ConsentsProblems.VersionConflict("consent");

        consent.Update(request.ToFields(), new AuditStamp(clock.GetUtcNow(), user.Name));
        if (!await store.UpdateAsync(consent, request.Version, ct))
            return ConsentsProblems.VersionConflict("consent");
        return TypedResults.Ok(ConsentDetails.From(record, LocalDate.Today(clock, locale.Value)));
    }
}
