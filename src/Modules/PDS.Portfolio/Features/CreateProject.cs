using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Portfolio.Data;
using PDS.Portfolio.Domain;
using PDS.Shared.Security;
using PDS.Shared.Settings;
using PDS.Shared.Web;

namespace PDS.Portfolio.Features;

internal static class CreateProject
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/projects", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<CreateProjectRequest>()
            .WithName("CreateProject");

    internal static async Task<Results<Created<ProjectDetails>, ValidationProblem>> Handle(
        CreateProjectRequest request,
        ProjectStore store,
        ICurrentUser user,
        TimeProvider clock,
        IOptions<LocaleOptions> locale,
        CancellationToken ct)
    {
        var currency = locale.Value.Currency;
        var project = Project.Create(request.ToFields(currency), new AuditStamp(clock.GetUtcNow(), user.Name));

        var result = await store.CreateAsync(project, ct);
        return result switch
        {
            SaveResult.Saved => TypedResults.Created($"/api/portfolio/projects/{project.Id.Value}", ProjectDetails.From(project, currency)),
            SaveResult.CodeTaken => PortfolioProblems.CodeTaken(),
            _ => throw new InvalidOperationException($"Project {project.Id} was not created: {result}."),
        };
    }
}
