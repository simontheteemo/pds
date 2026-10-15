using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace PDS.Reporting.Features;

internal sealed record ExportDatasetInfo(string Name, string Title, string Description);

internal static class ListExports
{
    public static void Map(RouteGroupBuilder group) => group.MapGet("", Handle).WithName("ListExports");

    internal static Ok<List<ExportDatasetInfo>> Handle(ExportCatalog catalog) =>
        TypedResults.Ok(catalog.All.Select(d => new ExportDatasetInfo(d.Name, d.Title, d.Description)).ToList());
}
