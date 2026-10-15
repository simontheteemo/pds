using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace PDS.Documents.Storage;

/// <summary>
/// Development stand-in for S3: files under a local directory, "presigned" URLs pointing at the anonymous
/// /api/documents/local-files endpoints, guarded by an HMAC signature (random key per process) and an expiry.
/// URLs are root-relative so the browser resolves them against the SPA origin (and so through the Vite proxy,
/// avoiding a cross-origin PUT to the API port).
/// </summary>
internal sealed class LocalFileStorage(IOptions<DocumentsOptions> options) : IFileStorage
{
    public const string RoutePrefix = "/api/documents/local-files";
    private const string TypeSuffix = ".type";
    private readonly byte[] _signingKey = RandomNumberGenerator.GetBytes(32);

    public string Root { get; } = Path.GetFullPath(
        options.Value.LocalPath is { Length: > 0 } path ? path : Path.Combine(Path.GetTempPath(), "pds-documents"));

    public Task<string> CreateUploadUrlAsync(string key, string contentType, DateTimeOffset expiresAt, CancellationToken ct)
    {
        var expires = expiresAt.ToUnixTimeSeconds();
        var sig = Sign("PUT", key, expires, contentType, string.Empty);
        return Task.FromResult($"{RoutePrefix}/{key}?expires={expires}&sig={sig}");
    }

    public Task<string> CreateDownloadUrlAsync(
        string key, string fileName, string contentType, DateTimeOffset expiresAt, CancellationToken ct)
    {
        var expires = expiresAt.ToUnixTimeSeconds();
        var sig = Sign("GET", key, expires, contentType, fileName);
        return Task.FromResult(
            $"{RoutePrefix}/{key}?expires={expires}&type={Uri.EscapeDataString(contentType)}" +
            $"&name={Uri.EscapeDataString(fileName)}&sig={sig}");
    }

    public Task<StoredObject?> HeadAsync(string key, CancellationToken ct)
    {
        var path = PathFor(key);
        if (path is null || !File.Exists(path))
            return Task.FromResult<StoredObject?>(null);
        var type = File.Exists(path + TypeSuffix) ? File.ReadAllText(path + TypeSuffix) : null;
        return Task.FromResult<StoredObject?>(new StoredObject(new FileInfo(path).Length, type));
    }

    public Task CopyAsync(string sourceKey, string destinationKey, CancellationToken ct)
    {
        var source = PathFor(sourceKey) ?? throw new InvalidOperationException("Invalid key.");
        var destination = PathFor(destinationKey) ?? throw new InvalidOperationException("Invalid key.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
        if (File.Exists(source + TypeSuffix))
            File.Copy(source + TypeSuffix, destination + TypeSuffix, overwrite: true);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        if (PathFor(key) is { } path)
        {
            File.Delete(path);
            File.Delete(path + TypeSuffix);
        }

        return Task.CompletedTask;
    }

    /// <summary>Writes an uploaded body and its content type (used by the local PUT endpoint).</summary>
    public async Task WriteAsync(string path, Stream body, string contentType, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using (var file = File.Create(path))
            await body.CopyToAsync(file, ct);
        await File.WriteAllTextAsync(path + TypeSuffix, contentType, ct);
    }

    /// <returns>The full path for a key, or null when it would resolve outside the root.</returns>
    public string? PathFor(string key)
    {
        var full = Path.GetFullPath(Path.Combine(Root, key));
        return full.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? full : null;
    }

    public bool Verify(string method, string key, long expires, string contentType, string fileName, string? sig, DateTimeOffset now)
    {
        if (sig is null || now.ToUnixTimeSeconds() > expires)
            return false;
        var expected = Encoding.ASCII.GetBytes(Sign(method, key, expires, contentType, fileName));
        return CryptographicOperations.FixedTimeEquals(expected, Encoding.ASCII.GetBytes(sig));
    }

    private string Sign(string method, string key, long expires, string contentType, string fileName) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(
            _signingKey,
            Encoding.UTF8.GetBytes(string.Join('\n', method, key, expires.ToString(CultureInfo.InvariantCulture), contentType, fileName))));
}
