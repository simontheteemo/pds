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

internal static class UpdateDocument
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPut("/{documentId:guid}", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<UpdateDocumentRequest>()
            .WithName("UpdateDocument");

    internal static async Task<Results<Ok<DocumentDetails>, NotFound, ValidationProblem, ProblemHttpResult>> Handle(
        Guid projectId, Guid documentId, UpdateDocumentRequest request, DocumentStore store,
        IPortfolioQueries portfolio, ICurrentUser user, TimeProvider clock, IOptions<DocumentsOptions> options,
        CancellationToken ct)
    {
        var project = await ProjectLookup.FindAsync(portfolio, projectId, ct);
        if (project is null)
            return TypedResults.NotFound();
        if (project.IsArchived)
            return DocumentsProblems.ProjectArchived();

        if (await store.GetAsync(new ProjectId(projectId), new DocumentId(documentId), ct) is not { Document.IsDeleted: false } record)
            return TypedResults.NotFound();

        var doc = record.Document;
        if (doc.Version != request.Version)
            return DocumentsProblems.VersionConflict();

        doc.Update(request.ToFields(options.Value.EffectiveCategories), new AuditStamp(clock.GetUtcNow(), user.Name));
        return await store.UpdateAsync(doc, request.Version, ct)
            ? TypedResults.Ok(DocumentDetails.From(record))
            : DocumentsProblems.VersionConflict();
    }
}
