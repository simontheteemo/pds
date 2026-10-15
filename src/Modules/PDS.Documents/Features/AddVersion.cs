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

internal static class AddVersion
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{documentId:guid}/versions", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<AddVersionRequest>()
            .WithName("AddDocumentVersion");

    internal static async Task<Results<Ok<DocumentDetails>, NotFound, ValidationProblem, ProblemHttpResult>> Handle(
        Guid projectId, Guid documentId, AddVersionRequest request, DocumentStore store, IFileStorage storage,
        IPortfolioQueries portfolio, ICurrentUser user, TimeProvider clock, IOptions<DocumentsOptions> options,
        ILoggerFactory loggers, CancellationToken ct)
    {
        var project = await ProjectLookup.FindAsync(portfolio, projectId, ct);
        if (project is null)
            return TypedResults.NotFound();
        if (project.IsArchived)
            return DocumentsProblems.ProjectArchived();

        var pid = new ProjectId(projectId);
        if (await store.GetAsync(pid, new DocumentId(documentId), ct) is not { Document.IsDeleted: false } record)
            return TypedResults.NotFound();

        var doc = record.Document;
        if (doc.Version != request.Version)
            return DocumentsProblems.VersionConflict();

        var stamp = new AuditStamp(clock.GetUtcNow(), user.Name);
        var (upload, problem) = await UploadConfirmation.ConfirmAsync(
            storage, options.Value.MaxFileSizeBytes, pid, doc.Id, request.UploadId, request.FileName,
            doc.CurrentVersion.Number + 1, stamp, loggers.CreateLogger(nameof(AddVersion)), ct);
        if (problem is not null)
            return problem;

        doc.AddVersion(upload!.Version, stamp);
        if (!await store.AddVersionAsync(doc, request.Version, ct))
            return DocumentsProblems.VersionConflict();

        await UploadConfirmation.DiscardPendingAsync(storage, upload.PendingKey, loggers.CreateLogger(nameof(AddVersion)), ct);
        return TypedResults.Ok(DocumentDetails.From(new DocumentRecord(doc, [upload.Version, .. record.Versions])));
    }
}
