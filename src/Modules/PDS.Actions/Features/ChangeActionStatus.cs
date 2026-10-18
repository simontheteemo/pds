using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Actions.Contracts;
using PDS.Actions.Data;
using PDS.Actions.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared.Security;
using PDS.Shared.Settings;
using PDS.Shared.Web;

namespace PDS.Actions.Features;

/// <summary>
/// Complete and reopen are open to the action's owner (any role) and to managers; cancel is managers only.
/// Archived projects block cancel but not complete or reopen.
/// </summary>
internal static class ChangeActionStatus
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{actionId:guid}/complete", Complete)
            .WithValidation<ActionVersionRequest>()
            .WithName("CompleteAction");
        group.MapPost("/{actionId:guid}/cancel", Cancel)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<ActionVersionRequest>()
            .WithName("CancelAction");
        group.MapPost("/{actionId:guid}/reopen", Reopen)
            .WithValidation<ActionVersionRequest>()
            .WithName("ReopenAction");
    }

    internal static Task<Results<Ok<ActionDetails>, NotFound, ValidationProblem, ProblemHttpResult>> Complete(
        Guid projectId, Guid actionId, ActionVersionRequest request, ActionStore store, IPortfolioQueries portfolio,
        ICurrentUser user, TimeProvider clock, IOptions<LocaleOptions> locale, CancellationToken ct) =>
        Change(
            projectId, actionId, request.Version, allowOwner: true, checkArchived: false,
            isNoOp: a => !a.IsOpen, apply: (a, stamp) => a.Complete(stamp),
            store, portfolio, user, clock, locale.Value, ct);

    internal static Task<Results<Ok<ActionDetails>, NotFound, ValidationProblem, ProblemHttpResult>> Cancel(
        Guid projectId, Guid actionId, ActionVersionRequest request, ActionStore store, IPortfolioQueries portfolio,
        ICurrentUser user, TimeProvider clock, IOptions<LocaleOptions> locale, CancellationToken ct) =>
        Change(
            projectId, actionId, request.Version, allowOwner: false, checkArchived: true,
            isNoOp: a => !a.IsOpen, apply: (a, stamp) => a.Cancel(stamp),
            store, portfolio, user, clock, locale.Value, ct);

    internal static Task<Results<Ok<ActionDetails>, NotFound, ValidationProblem, ProblemHttpResult>> Reopen(
        Guid projectId, Guid actionId, ActionVersionRequest request, ActionStore store, IPortfolioQueries portfolio,
        ICurrentUser user, TimeProvider clock, IOptions<LocaleOptions> locale, CancellationToken ct) =>
        Change(
            projectId, actionId, request.Version, allowOwner: true, checkArchived: false,
            isNoOp: a => a.IsOpen, apply: (a, stamp) => a.Reopen(stamp),
            store, portfolio, user, clock, locale.Value, ct);

    private static async Task<Results<Ok<ActionDetails>, NotFound, ValidationProblem, ProblemHttpResult>> Change(
        Guid projectId, Guid actionId, long version, bool allowOwner, bool checkArchived,
        Func<ActionItem, bool> isNoOp, Func<ActionItem, AuditStamp, bool> apply, ActionStore store,
        IPortfolioQueries portfolio, ICurrentUser user, TimeProvider clock, LocaleOptions locale, CancellationToken ct)
    {
        var action = await store.GetAsync(new ProjectId(projectId), new ActionId(actionId), ct);
        if (action is null)
            return TypedResults.NotFound();

        var isManager = user.Roles.Contains(Roles.Manager) || user.Roles.Contains(Roles.Admin);
        var isOwner = string.Equals(user.Id, action.Fields.OwnerId, StringComparison.Ordinal);
        if (!(isManager || (allowOwner && isOwner)))
            return ActionsProblems.NotOwner();

        var today = LocalDate.Today(clock, locale);
        if (isNoOp(action))
            return TypedResults.Ok(ActionDetails.From(action, today));
        if (action.Version != version)
            return ActionsProblems.VersionConflict();
        if (checkArchived && await ProjectLookup.FindAsync(portfolio, projectId, ct) is not { IsArchived: false })
            return ActionsProblems.ProjectArchived();
        if (!apply(action, new AuditStamp(clock.GetUtcNow(), user.Name)))
            return TypedResults.Ok(ActionDetails.From(action, today));

        return await store.UpdateAsync(action, version, ct)
            ? TypedResults.Ok(ActionDetails.From(action, today))
            : ActionsProblems.VersionConflict();
    }
}
