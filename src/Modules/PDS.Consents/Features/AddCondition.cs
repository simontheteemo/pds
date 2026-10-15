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
using Condition = PDS.Consents.Domain.Condition;

namespace PDS.Consents.Features;

internal static class AddCondition
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{consentId:guid}/conditions", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<CreateConditionRequest>()
            .WithName("AddCondition");

    internal static async Task<Results<Created<ConditionDetails>, NotFound, ValidationProblem>> Handle(
        Guid projectId, Guid consentId, CreateConditionRequest request, ConsentStore store, IPortfolioQueries portfolio,
        ICurrentUser user, TimeProvider clock, IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var project = await ProjectLookup.FindAsync(portfolio, projectId, ct);
        if (project is null)
            return TypedResults.NotFound();
        if (project.IsArchived)
            return ConsentsProblems.ProjectArchived();

        var record = await store.GetAsync(new ProjectId(projectId), new ConsentId(consentId), ct);
        if (record is null)
            return TypedResults.NotFound();

        var condition = Condition.Create(
            new ProjectId(projectId), new ConsentId(consentId), request.ToFields(), new AuditStamp(clock.GetUtcNow(), user.Name));
        await store.CreateConditionAsync(condition, ct);
        return TypedResults.Created(
            $"/api/consents/projects/{projectId}/consents/{consentId}",
            ConditionDetails.From(condition, record.Consent.IsActive, LocalDate.Today(clock, locale.Value)));
    }
}
