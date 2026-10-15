namespace PDS.Documents.Domain;

/// <summary>One uploaded file. Immutable once written.</summary>
internal sealed record DocumentVersion(
    int Number, string FileName, string ContentType, long SizeBytes, string StorageKey, AuditStamp Uploaded);
