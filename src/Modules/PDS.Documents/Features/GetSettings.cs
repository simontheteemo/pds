using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Documents.Storage;

namespace PDS.Documents.Features;

internal sealed record DocumentSettings(IReadOnlyList<string> Categories, long MaxFileSizeBytes, IReadOnlyList<string> AllowedExtensions);

internal static class GetSettings
{
    public static void Map(RouteGroupBuilder group) => group.MapGet("/settings", Handle).WithName("GetDocumentSettings");

    internal static Ok<DocumentSettings> Handle(IOptions<DocumentsOptions> options) =>
        TypedResults.Ok(new DocumentSettings(options.Value.EffectiveCategories, options.Value.MaxFileSizeBytes, FileTypes.Extensions));
}
