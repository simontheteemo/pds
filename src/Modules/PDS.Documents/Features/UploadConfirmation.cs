using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using PDS.Documents.Contracts;
using PDS.Documents.Domain;
using PDS.Documents.Storage;
using PDS.Portfolio.Contracts;

namespace PDS.Documents.Features;

internal sealed record ConfirmedUpload(DocumentVersion Version, string PendingKey);

/// <summary>
/// Checks a pending upload and copies it to its permanent key (server-side; no bytes through the API).
/// Order: Head → size/type checks → Copy. The caller writes metadata, then calls <see cref="DiscardPendingAsync"/>.
/// </summary>
internal static class UploadConfirmation
{
    public static async Task<(ConfirmedUpload? Upload, ValidationProblem? Problem)> ConfirmAsync(
        IFileStorage storage, long maxFileSizeBytes, ProjectId projectId, DocumentId documentId, Guid uploadId,
        string fileName, int number, AuditStamp stamp, CancellationToken ct)
    {
        var cleanName = FileTypes.CleanFileName(fileName);
        if (FileTypes.ContentTypeFor(cleanName) is not { } contentType)
            return (null, DocumentsProblems.FileTypeNotAllowed());

        var pendingKey = StorageKeys.Pending(projectId, uploadId);
        if (await storage.HeadAsync(pendingKey, ct) is not { } stored)
            return (null, DocumentsProblems.UploadMissing());

        ValidationProblem? problem =
            stored.SizeBytes <= 0 ? DocumentsProblems.Empty()
            : stored.SizeBytes > maxFileSizeBytes ? DocumentsProblems.TooLarge(maxFileSizeBytes)
            : !string.Equals(stored.ContentType, contentType, StringComparison.OrdinalIgnoreCase) ? DocumentsProblems.TypeMismatch()
            : null;
        if (problem is not null)
        {
            await storage.DeleteAsync(pendingKey, ct);
            return (null, problem);
        }

        var fileKey = StorageKeys.File(projectId, documentId, uploadId);
        await storage.CopyAsync(pendingKey, fileKey, ct);
        return (new ConfirmedUpload(new DocumentVersion(number, cleanName, contentType, stored.SizeBytes, fileKey, stamp), pendingKey), null);
    }

    /// <summary>Best effort: the pending/ lifecycle rule removes it anyway.</summary>
    public static async Task DiscardPendingAsync(IFileStorage storage, string pendingKey, ILogger logger, CancellationToken ct)
    {
        try
        {
            await storage.DeleteAsync(pendingKey, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not delete pending upload {Key}; the lifecycle rule will expire it.", pendingKey);
        }
    }
}
