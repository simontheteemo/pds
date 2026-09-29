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
using PDS.Shared.Web;

namespace PDS.Portfolio.Features;

internal static class UpdateProject
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPut("/projects/{id:guid}", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<UpdateProjectRequest>()
            .WithName("UpdateProject");

    internal static async Task<Results<Ok<ProjectDetails>, NotFound, ValidationProblem, ProblemHttpResult>> Handle(
        Guid id,
        UpdateProjectRequest request,
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
        var previousCode = project.Fields.Code;
        project.Update(request.ToFields(currency), new AuditStamp(clock.GetUtcNow(), user.Name));

        return await store.UpdateAsync(project, request.Version, previousCode, ct) switch
        {
            SaveResult.Saved => TypedResults.Ok(ProjectDetails.From(project, currency)),
            SaveResult.CodeTaken => PortfolioProblems.CodeTaken(),
            _ => PortfolioProblems.VersionConflict(),
        };
    }
}
