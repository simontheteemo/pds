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

    internal static async Task<Results<Created<DocumentDetails>, NotFound, ValidationProblem>> Handle(
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
        var documentId = DocumentId.New();
        var stamp = new AuditStamp(clock.GetUtcNow(), user.Name);
        var (upload, problem) = await UploadConfirmation.ConfirmAsync(
            storage, options.Value.MaxFileSizeBytes, pid, documentId, request.UploadId, request.FileName, 1, stamp,
            loggers.CreateLogger(nameof(CreateDocument)), ct);
        if (problem is not null)
            return problem;

        var document = Document.Create(documentId, pid, request.ToFields(options.Value.EffectiveCategories), upload!.Version, stamp);
        await store.CreateAsync(document, ct);
        await UploadConfirmation.DiscardPendingAsync(storage, upload.PendingKey, loggers.CreateLogger(nameof(CreateDocument)), ct);
        return TypedResults.Created(
            $"/api/documents/projects/{projectId}/documents/{documentId.Value}",
            DocumentDetails.From(new DocumentRecord(document, [upload.Version])));
    }
}
