using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Portfolio.Contracts;
using PDS.Portfolio.Data;
using PDS.Portfolio.Domain;
using PDS.Shared.Security;
using PDS.Shared.Settings;

namespace PDS.Portfolio.Features;

internal sealed record VersionRequest(long Version);

internal static class ArchiveProject
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/projects/{id:guid}/archive",
                (Guid id, VersionRequest request, ProjectStore store, ICurrentUser user, TimeProvider clock, IOptions<LocaleOptions> locale, CancellationToken ct) =>
                    SetArchived(id, archived: true, request, store, user, clock, locale, ct))
            .RequireAuthorization(Policies.CanAdminister)
            .WithName("ArchiveProject");
        group.MapPost("/projects/{id:guid}/restore",
                (Guid id, VersionRequest request, ProjectStore store, ICurrentUser user, TimeProvider clock, IOptions<LocaleOptions> locale, CancellationToken ct) =>
                    SetArchived(id, archived: false, request, store, user, clock, locale, ct))
            .RequireAuthorization(Policies.CanAdminister)
            .WithName("RestoreProject");
    }

    internal static async Task<Results<Ok<ProjectDetails>, NotFound, ProblemHttpResult>> SetArchived(
        Guid id,
        bool archived,
        VersionRequest request,
        ProjectStore store,
        ICurrentUser user,
        TimeProvider clock,
        IOptions<LocaleOptions> locale,
        CancellationToken ct)
    {
        var project = await store.GetAsync(new ProjectId(id), ct);
        if (project is null)
            return TypedResults.NotFound();
        if (project.Version != request.Version)
            return PortfolioProblems.VersionConflict();

        var currency = locale.Value.Currency;
        if (!project.SetArchived(archived, new AuditStamp(clock.GetUtcNow(), user.Name)))
            return TypedResults.Ok(ProjectDetails.From(project, currency));

        return await store.UpdateAsync(project, request.Version, project.Fields.Code, ct) == SaveResult.Saved
            ? TypedResults.Ok(ProjectDetails.From(project, currency))
            : PortfolioProblems.VersionConflict();
    }
}
