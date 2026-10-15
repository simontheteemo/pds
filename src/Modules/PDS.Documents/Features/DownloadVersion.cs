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

internal static class DownloadVersion
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{documentId:guid}/versions/{number:int}/download", Handle).WithName("DownloadDocumentVersion");

    internal static async Task<Results<Ok<DownloadLink>, NotFound>> Handle(
        Guid projectId, Guid documentId, int number, DocumentStore store, IFileStorage storage,
        TimeProvider clock, CancellationToken ct)
    {
        if (await store.GetAsync(new ProjectId(projectId), new DocumentId(documentId), ct) is not { Document.IsDeleted: false } record)
            return TypedResults.NotFound();

        var version = record.Versions.FirstOrDefault(v => v.Number == number)
            ?? (record.Document.CurrentVersion.Number == number ? record.Document.CurrentVersion : null);
        if (version is null)
            return TypedResults.NotFound();

        var expiresAt = clock.GetUtcNow().Add(Lifetime);
        var url = await storage.CreateDownloadUrlAsync(version.StorageKey, version.FileName, version.ContentType, expiresAt, ct);
        return TypedResults.Ok(new DownloadLink(url, expiresAt));
    }
}
