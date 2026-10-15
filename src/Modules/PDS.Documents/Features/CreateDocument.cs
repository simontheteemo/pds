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

internal static class CreateDocument
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<CreateDocumentRequest>()
            .WithName("CreateDocument");

    internal static async Task<Results<Created<DocumentDetails>, NotFound, ValidationProblem, ProblemHttpResult>> Handle(
        Guid projectId, CreateDocumentRequest request, DocumentStore store, IFileStorage storage,
        IPortfolioQueries portfolio, ICurrentUser user, TimeProvider clock, IOptions<DocumentsOptions> options,
        ILoggerFactory loggers, CancellationToken ct)
    {
        var project = await ProjectLookup.FindAsync(portfolio, projectId, ct);
        if (project is null)
            return TypedResults.NotFound();
        if (project.IsArchived)
            return DocumentsProblems.ProjectArchived();

        var pid = new ProjectId(projectId);
        var logger = loggers.CreateLogger(nameof(CreateDocument));

        // Replay of an already-confirmed upload (its pending object may be gone by now).
        if (await store.FindByUploadAsync(pid, request.UploadId, ct) is { } claimedId)
            return await ExistingAsync(store, pid, claimedId, ct);

        var documentId = DocumentId.New();
        var stamp = new AuditStamp(clock.GetUtcNow(), user.Name);
        ConfirmedUpload? upload;
        ValidationProblem? problem;
        try
        {
            (upload, problem) = await UploadConfirmation.ConfirmAsync(
                storage, options.Value.MaxFileSizeBytes, pid, documentId, request.UploadId, request.FileName, 1, stamp, logger, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A concurrent confirm may have committed and deleted the pending object mid-copy.
            if (await store.FindByUploadAsync(pid, request.UploadId, ct) is { } racedId)
                return await ExistingAsync(store, pid, racedId, ct);
            throw;
        }

        if (problem is not null)
        {
            // Same race, but the pending object was already gone at Head: answer with the winner if there is one.
            return await store.FindByUploadAsync(pid, request.UploadId, ct) is { } racedId
                ? await ExistingAsync(store, pid, racedId, ct)
                : problem;
        }

        var document = Document.Create(documentId, pid, request.ToFields(options.Value.EffectiveCategories), upload!.Version, stamp);
        if (!await store.CreateAsync(document, request.UploadId, ct))
        {
            // Lost (or conflicted with) a concurrent confirm of the same upload: drop our copy and answer with the winner.
            // A transient failure of this claim read surfaces as a 500; the winner's document is unaffected.
            await DiscardCopyAsync(storage, upload.Version.StorageKey, logger, ct);
            return await store.FindByUploadAsync(pid, request.UploadId, ct) is { } winnerId
                ? await ExistingAsync(store, pid, winnerId, ct)
                : DocumentsProblems.UploadInProgress();
        }

        await UploadConfirmation.DiscardPendingAsync(storage, upload.PendingKey, logger, ct);
        return ToCreated(projectId, new DocumentRecord(document, [upload.Version]));
    }

    private static async Task<Results<Created<DocumentDetails>, NotFound, ValidationProblem, ProblemHttpResult>> ExistingAsync(
        DocumentStore store, ProjectId projectId, DocumentId documentId, CancellationToken ct) =>
        await store.GetAsync(projectId, documentId, ct) is { Document.Deleted: null } record
            ? ToCreated(projectId.Value, record)
            : DocumentsProblems.UploadMissing();

    private static Created<DocumentDetails> ToCreated(Guid projectId, DocumentRecord record) =>
        TypedResults.Created(
            $"/api/documents/projects/{projectId}/documents/{record.Document.Id.Value}",
            DocumentDetails.From(record));

    private static async Task DiscardCopyAsync(IFileStorage storage, string key, ILogger logger, CancellationToken ct)
    {
        try
        {
            await storage.DeleteAsync(key, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not delete duplicate upload copy {Key}.", key);
        }
    }
}
