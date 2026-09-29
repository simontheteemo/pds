using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Portfolio.Contracts;
using PDS.Portfolio.Data;
using PDS.Shared.Settings;

namespace PDS.Portfolio.Features;

internal static class GetProject
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/projects/{id:guid}", Handle).WithName("GetProject");

    internal static async Task<Results<Ok<ProjectDetails>, NotFound>> Handle(
        Guid id, ProjectStore store, IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var project = await store.GetAsync(new ProjectId(id), ct);
        if (project is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(ProjectDetails.From(project, locale.Value.Currency));
    }
}
