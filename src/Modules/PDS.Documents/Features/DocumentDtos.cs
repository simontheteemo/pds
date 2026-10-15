using PDS.Documents.Data;
using PDS.Documents.Domain;

namespace PDS.Documents.Features;

/// <summary>Editable metadata shared by create and update.</summary>
internal interface IDocumentMetadataInput
{
    string Title { get; }

    string Category { get; }

    DocumentLink? Link { get; }

    IReadOnlyList<string>? Tags { get; }

    string? Notes { get; }
}

internal sealed record CreateDocumentRequest(
    Guid UploadId, string FileName, string Title, string Category, DocumentLink? Link, IReadOnlyList<string>? Tags, string? Notes)
    : IDocumentMetadataInput;

internal sealed record UpdateDocumentRequest(
    string Title, string Category, DocumentLink? Link, IReadOnlyList<string>? Tags, string? Notes, long Version)
    : IDocumentMetadataInput;

internal sealed record AddVersionRequest(Guid UploadId, string FileName, long Version);

internal sealed record DeleteDocumentRequest(long Version);

internal sealed record DownloadLink(string Url, DateTimeOffset ExpiresAt);

internal static class DocumentInputMapping
{
    /// <summary>Call only after validation.</summary>
    public static DocumentFields ToFields(this IDocumentMetadataInput i, IReadOnlyList<string> categories) =>
        DocumentRules.Normalise(new DocumentFields(i.Title, i.Category, i.Link, i.Tags ?? [], i.Notes), categories);
}

internal sealed record VersionDetails(
    int Number, string FileName, string ContentType, long SizeBytes, DateTimeOffset UploadedAt, string UploadedBy)
{
    public static VersionDetails From(DocumentVersion v) =>
        new(v.Number, v.FileName, v.ContentType, v.SizeBytes, v.Uploaded.At, v.Uploaded.By);
}

internal sealed record DocumentDetails(
    Guid Id,
    Guid ProjectId,
    string Title,
    string Category,
    DocumentLink? Link,
    IReadOnlyList<string> Tags,
    string? Notes,
    int CurrentVersion,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAt,
    string UploadedBy,
    IReadOnlyList<VersionDetails> Versions,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    long Version)
{
    public static DocumentDetails From(DocumentRecord record)
    {
        var d = record.Document;
        var current = d.CurrentVersion;
        var versions = record.Versions.Count > 0 ? record.Versions : [current];
        return new DocumentDetails(
            d.Id.Value, d.ProjectId.Value, d.Fields.Title, d.Fields.Category, d.Fields.Link, d.Fields.Tags, d.Fields.Notes,
            current.Number, current.FileName, current.ContentType, current.SizeBytes, current.Uploaded.At, current.Uploaded.By,
            versions.Select(VersionDetails.From).ToList(),
            d.Created.At, d.Created.By, d.Updated.At, d.Updated.By, d.Version);
    }
}
