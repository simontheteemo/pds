namespace PDS.Documents.Storage;

internal sealed record StoredObject(long SizeBytes, string? ContentType);

/// <summary>Object storage for document files. Implementations never stream file bytes through the API in AWS.</summary>
internal interface IFileStorage
{
    /// <summary>A URL the browser PUTs the file to, with exactly this Content-Type header.</summary>
    Task<string> CreateUploadUrlAsync(string key, string contentType, DateTimeOffset expiresAt, CancellationToken ct);

    /// <summary>A URL that downloads the object as an attachment named <paramref name="fileName"/>.</summary>
    Task<string> CreateDownloadUrlAsync(string key, string fileName, string contentType, DateTimeOffset expiresAt, CancellationToken ct);

    /// <returns>Size and content type, or null when the object does not exist.</returns>
    Task<StoredObject?> HeadAsync(string key, CancellationToken ct);

    Task CopyAsync(string sourceKey, string destinationKey, CancellationToken ct);

    Task DeleteAsync(string key, CancellationToken ct);
}
