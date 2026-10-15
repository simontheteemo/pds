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

internal static class CreateConsent
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<CreateConsentRequest>()
            .WithName("CreateConsent");

    internal static async Task<Results<Created<ConsentDetails>, NotFound, ValidationProblem>> Handle(
        Guid projectId, CreateConsentRequest request, ConsentStore store, IPortfolioQueries portfolio,
        ICurrentUser user, TimeProvider clock, IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var project = await ProjectLookup.FindAsync(portfolio, projectId, ct);
        if (project is null)
            return TypedResults.NotFound();
        if (project.IsArchived)
            return ConsentsProblems.ProjectArchived();

        var consent = Consent.Create(new ProjectId(projectId), request.ToFields(), new AuditStamp(clock.GetUtcNow(), user.Name));
        await store.CreateAsync(consent, ct);
        var today = LocalDate.Today(clock, locale.Value);
        return TypedResults.Created(
            $"/api/consents/projects/{projectId}/consents/{consent.Id.Value}",
            ConsentDetails.From(new ConsentWithConditions(consent, []), today));
    }
}
