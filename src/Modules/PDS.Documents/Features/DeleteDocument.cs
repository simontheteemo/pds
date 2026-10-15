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

internal static class DeleteDocument
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{documentId:guid}/delete", Handle)
            .RequireAuthorization(Policies.CanAdminister)
            .WithValidation<DeleteDocumentRequest>()
            .WithName("DeleteDocument");

    internal static async Task<Results<NoContent, NotFound, ProblemHttpResult>> Handle(
        Guid projectId, Guid documentId, DeleteDocumentRequest request, DocumentStore store,
        ICurrentUser user, TimeProvider clock, CancellationToken ct)
    {
        if (await store.GetAsync(new ProjectId(projectId), new DocumentId(documentId), ct) is not { Document.IsDeleted: false } record)
            return TypedResults.NotFound();

        var doc = record.Document;
        if (doc.Version != request.Version)
            return DocumentsProblems.VersionConflict();

        doc.MarkDeleted(new AuditStamp(clock.GetUtcNow(), user.Name));
        return await store.UpdateAsync(doc, request.Version, ct)
            ? TypedResults.NoContent()
            : DocumentsProblems.VersionConflict();
    }
}
