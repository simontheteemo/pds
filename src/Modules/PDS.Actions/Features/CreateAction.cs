using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Actions.Data;
using PDS.Actions.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared.People;
using PDS.Shared.Security;
using PDS.Shared.Settings;
using PDS.Shared.Web;

namespace PDS.Actions.Features;

internal static class CreateAction
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<CreateActionRequest>()
            .WithName("CreateAction");

    internal static async Task<Results<Created<ActionDetails>, NotFound, ValidationProblem>> Handle(
        Guid projectId, CreateActionRequest request, ActionStore store, IPortfolioQueries portfolio,
        IPeopleDirectory people, ICurrentUser user, TimeProvider clock, IOptions<LocaleOptions> locale,
        CancellationToken ct)
    {
        var project = await ProjectLookup.FindAsync(portfolio, projectId, ct);
        if (project is null)
            return TypedResults.NotFound();
        if (project.IsArchived)
            return ActionsProblems.ProjectArchived();
        if (await OwnerResolver.ResolveAsync(people, request.OwnerId!, null, ct) is not { } owner)
            return ActionsProblems.UnknownOwner();

        var action = ActionItem.Create(
            new ProjectId(projectId), request.ToFields(owner.Id, owner.Name), new AuditStamp(clock.GetUtcNow(), user.Name));
        await store.CreateAsync(action, ct);
        return TypedResults.Created(
            $"/api/actions/projects/{projectId}/actions/{action.Id.Value}",
            ActionDetails.From(action, LocalDate.Today(clock, locale.Value)));
    }
}
