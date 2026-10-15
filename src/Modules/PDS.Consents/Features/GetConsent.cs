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

internal static class GetConsent
{
    public static void Map(RouteGroupBuilder group) => group.MapGet("/{consentId:guid}", Handle).WithName("GetConsent");

    internal static async Task<Results<Ok<ConsentDetails>, NotFound>> Handle(
        Guid projectId, Guid consentId, ConsentStore store, TimeProvider clock,
        IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var record = await store.GetAsync(new ProjectId(projectId), new ConsentId(consentId), ct);
        return record is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(ConsentDetails.From(record, LocalDate.Today(clock, locale.Value)));
    }
}
