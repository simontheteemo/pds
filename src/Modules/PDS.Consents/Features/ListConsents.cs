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

internal static class ListConsents
{
    public static void Map(RouteGroupBuilder group) => group.MapGet("", Handle).WithName("ListConsents");

    internal static async Task<Results<Ok<List<ConsentDetails>>, NotFound>> Handle(
        Guid projectId, ConsentStore store, IPortfolioQueries portfolio, TimeProvider clock,
        IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        if (await ProjectLookup.FindAsync(portfolio, projectId, ct) is null)
            return TypedResults.NotFound();

        var today = LocalDate.Today(clock, locale.Value);
        var records = await store.ListForProjectAsync(new ProjectId(projectId), ct);
        var ordered = records
            .OrderBy(r => !r.Consent.IsActive)
            .ThenBy(r => r.Consent.Fields.Type)
            .ThenBy(r => r.Consent.Fields.Title, StringComparer.OrdinalIgnoreCase)
            .Select(r => ConsentDetails.From(r, today))
            .ToList();
        return TypedResults.Ok(ordered);
    }
}
