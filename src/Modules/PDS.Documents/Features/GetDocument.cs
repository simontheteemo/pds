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

internal static class GetDocument
{
    public static void Map(RouteGroupBuilder group) => group.MapGet("/{documentId:guid}", Handle).WithName("GetDocument");

    internal static async Task<Results<Ok<DocumentDetails>, NotFound>> Handle(
        Guid projectId, Guid documentId, DocumentStore store, CancellationToken ct) =>
        await store.GetAsync(new ProjectId(projectId), new DocumentId(documentId), ct) is { Document.IsDeleted: false } record
            ? TypedResults.Ok(DocumentDetails.From(record))
            : TypedResults.NotFound();
}
