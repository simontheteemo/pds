using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Actions.Contracts;
using PDS.Actions.Data;
using PDS.Actions.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared.People;
using PDS.Shared.Security;
using PDS.Shared.Settings;
using PDS.Shared.Web;

namespace PDS.Actions.Features;

internal static class UpdateAction
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPut("/{actionId:guid}", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<UpdateActionRequest>()
            .WithName("UpdateAction");

    internal static async Task<Results<Ok<ActionDetails>, NotFound, ValidationProblem, ProblemHttpResult>> Handle(
        Guid projectId, Guid actionId, UpdateActionRequest request, ActionStore store, IPortfolioQueries portfolio,
        IPeopleDirectory people, ICurrentUser user, TimeProvider clock, IOptions<LocaleOptions> locale,
        CancellationToken ct)
    {
        var action = await store.GetAsync(new ProjectId(projectId), new ActionId(actionId), ct);
        if (action is null)
            return TypedResults.NotFound();
        if (action.Version != request.Version)
            return ActionsProblems.VersionConflict();
        if (!action.IsOpen)
            return ActionsProblems.ActionClosed();
        if (await ProjectLookup.FindAsync(portfolio, projectId, ct) is not { IsArchived: false })
            return ActionsProblems.ProjectArchived();
        if (await OwnerResolver.ResolveAsync(people, request.OwnerId!, action.Fields, ct) is not { } owner)
            return ActionsProblems.UnknownOwner();

        action.Update(
            request.ToFields(owner.Id, owner.Name), request.Status!.Value, new AuditStamp(clock.GetUtcNow(), user.Name));
        return await store.UpdateAsync(action, request.Version, ct)
            ? TypedResults.Ok(ActionDetails.From(action, LocalDate.Today(clock, locale.Value)))
            : ActionsProblems.VersionConflict();
    }
}
