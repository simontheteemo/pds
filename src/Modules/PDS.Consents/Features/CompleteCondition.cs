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

internal static class CompleteCondition
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{consentId:guid}/conditions/{conditionId:guid}/complete", Complete)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<CompleteConditionRequest>()
            .WithName("CompleteCondition");
        group.MapPost("/{consentId:guid}/conditions/{conditionId:guid}/reopen", Reopen)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<ReopenConditionRequest>()
            .WithName("ReopenCondition");
    }

    internal static Task<Results<Ok<ConditionDetails>, NotFound, ValidationProblem, ProblemHttpResult>> Complete(
        Guid projectId, Guid consentId, Guid conditionId, CompleteConditionRequest request, ConsentStore store,
        ICurrentUser user, TimeProvider clock, IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var today = LocalDate.Today(clock, locale.Value);
        var completedOn = request.CompletedOn!.Value;
        return Change(
            projectId, consentId, conditionId, request.Version, store,
            isNoOp: c => c.CompletedOn is not null,
            precondition: () => completedOn > today ? ConsentsProblems.CompletedInFuture() : null,
            apply: (c, stamp) => c.Complete(completedOn, stamp),
            user, clock, today, ct);
    }

    internal static Task<Results<Ok<ConditionDetails>, NotFound, ValidationProblem, ProblemHttpResult>> Reopen(
        Guid projectId, Guid consentId, Guid conditionId, ReopenConditionRequest request, ConsentStore store,
        ICurrentUser user, TimeProvider clock, IOptions<LocaleOptions> locale, CancellationToken ct) =>
        Change(
            projectId, consentId, conditionId, request.Version, store,
            isNoOp: c => c.CompletedOn is null,
            precondition: () => null,
            apply: (c, stamp) => c.Reopen(stamp),
            user, clock, LocalDate.Today(clock, locale.Value), ct);

    private static async Task<Results<Ok<ConditionDetails>, NotFound, ValidationProblem, ProblemHttpResult>> Change(
        Guid projectId, Guid consentId, Guid conditionId, long version, ConsentStore store, Func<Condition, bool> isNoOp,
        Func<ValidationProblem?> precondition, Func<Condition, AuditStamp, bool> apply, ICurrentUser user,
        TimeProvider clock, DateOnly today, CancellationToken ct)
    {
        var record = await store.GetAsync(new ProjectId(projectId), new ConsentId(consentId), ct);
        if (record is null)
            return TypedResults.NotFound();
        var condition = await store.GetConditionAsync(
            new ProjectId(projectId), new ConsentId(consentId), new ConditionId(conditionId), ct);
        if (condition is null)
            return TypedResults.NotFound();

        var active = record.Consent.IsActive;
        if (isNoOp(condition))
            return TypedResults.Ok(ConditionDetails.From(condition, active, today));
        if (condition.Version != version)
            return ConsentsProblems.VersionConflict("condition");
        if (precondition() is { } problem)
            return problem;
        if (!apply(condition, new AuditStamp(clock.GetUtcNow(), user.Name)))
            return TypedResults.Ok(ConditionDetails.From(condition, active, today));

        return await store.UpdateConditionAsync(condition, version, ct)
            ? TypedResults.Ok(ConditionDetails.From(condition, active, today))
            : ConsentsProblems.VersionConflict("condition");
    }
}
