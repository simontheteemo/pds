using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PDS.Documents.Contracts;
using PDS.Documents.Data;
using PDS.Documents.Domain;
using PDS.Documents.Storage;
using PDS.Portfolio.Contracts;
using PDS.Shared.Security;
using PDS.Shared.Web;

namespace PDS.Documents.Features;

internal static class ListDocuments
{
    public static void Map(RouteGroupBuilder group) => group.MapGet("", Handle).WithName("ListDocuments");

    internal static async Task<Results<Ok<List<DocumentDetails>>, NotFound>> Handle(
        Guid projectId, LinkedModule? linkModule, Guid? linkRecordId, DocumentStore store, IPortfolioQueries portfolio, CancellationToken ct)
    {
        if (await ProjectLookup.FindAsync(portfolio, projectId, ct) is null)
            return TypedResults.NotFound();
        var records = await store.ListForProjectAsync(new ProjectId(projectId), ct);
        return TypedResults.Ok(records
            .Where(r => !r.Document.IsDeleted)
            .Where(r => linkModule is null || r.Document.Fields.Link?.Module == linkModule)
            .Where(r => linkRecordId is null || r.Document.Fields.Link?.RecordId == linkRecordId)
            .OrderByDescending(r => r.Document.CurrentVersion.Uploaded.At)
            .Select(DocumentDetails.From)
            .ToList());
    }
}
