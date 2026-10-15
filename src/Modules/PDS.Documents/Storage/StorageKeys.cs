using PDS.Documents.Contracts;
using PDS.Portfolio.Contracts;

namespace PDS.Documents.Storage;

/// <summary>pending/ holds unconfirmed uploads (lifecycle-expired after 1 day); files/ holds confirmed versions.</summary>
internal static class StorageKeys
{
    public static string Pending(ProjectId projectId, Guid uploadId) => $"pending/{projectId.Value}/{uploadId}";

    public static string File(ProjectId projectId, DocumentId documentId, Guid uploadId) =>
        $"files/{projectId.Value}/{documentId.Value}/{uploadId}";
}
