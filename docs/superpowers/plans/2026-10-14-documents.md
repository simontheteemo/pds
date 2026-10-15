# PDS Documents (v1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A per-project document register. Files go browser → S3 through presigned URLs, metadata goes in the module's own DynamoDB table, and each document has a simple version history.

**Architecture:**
- A new module, `src/Modules/PDS.Documents`, owns the `documents` table:
  - document items: `PROJECT#pid` / `DOC#did`
  - version items: `PROJECT#pid` / `DOC#did#V#000001`
- Files go through a small `IFileStorage` interface:
  - `S3FileStorage` in AWS
  - `LocalFileStorage` with signed local endpoints in Development only
- Upload flow:
  1. Ask for a presigned `PUT` to `pending/<pid>/<uploadId>`.
  2. The browser `PUT`s the file.
  3. Confirm: `HeadObject` checks, server-side copy to `files/<pid>/<did>/<uploadId>`, metadata write, then delete the pending object.

  A lifecycle rule expires `pending/` after 1 day.
- CDK adds a private bucket construct to the app stack.
- The web app adds `web/src/features/documents`, a section on the project detail page.

**Tech Stack:** Same as the foundation. One new package: `AWSSDK.S3` 4.0.103.4. It's the official SDK, needed to sign URLs and call S3, and it has the same `AWSSDK.Core` floor as the existing `AWSSDK.DynamoDBv2` 4.0.106.

**Spec:** `docs/superpowers/specs/2026-10-14-documents-design.md`.

**Reference implementations to mirror:**
- Backend: `src/Modules/PDS.Consents`. Use it for the module shape, the project-scoped group, the store with `itemType` grouping, `ProjectLookup`, the `*Problems` class, DTO records and validators.
- Web:
  - `web/src/features/risks`: section card, keyed inner modal component, form model with Zod.
  - `web/src/shared/mutationError.ts` (`handleMutationError`): 400/409 handling. It's already on this branch; use it and **don't copy it**.

## Global Constraints

- Foundation constraints apply:
  - net10.0 and central package versions (`Directory.Packages.props`).
  - Only `Contracts/*` and `DocumentsModule` are public; everything else is `internal`.
  - `pk`/`sk` string keys.
  - ProblemDetails, and camelCase JSON with string enums.
- **The only new package is `AWSSDK.S3` 4.0.103.4.** No web packages.
- **No new tests** (user directive). Verify with:
  - `dotnet format PDS.slnx`, then `dotnet build PDS.slnx` (0 warnings, 0 errors). Existing tests must still compile.
  - In `web/`: `npm run gen:api && npm run typecheck && npm run lint && npm run build`. Run `npm ci` first if `node_modules` is missing.
  - Infra:
    - `dotnet publish src/PDS.Api -c Release -r linux-arm64 --self-contained false -o artifacts/api`
    - then `cd infra && npx --yes aws-cdk@2.1143.0 synth -c deployment=example-dev --quiet`
    - `web/dist` must exist, so run the web build first.
  - The manual API checks each task names.
- **Run the API on port 5086:** `ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/PDS.Api --urls http://localhost:5086`. The dev user is Admin.
- **DynamoDB Local is shared on :8000.** Reuse it (`curl -s localhost:8000`); never stop it.
- **If you run Vite**, use `npx vite --port 5196` with the proxy target overridden through the CLI or env only. Don't commit a port change.
- **Shared wiring files get one-line additive edits only:** `PDS.slnx`, `src/PDS.Api/Program.cs`, `src/PDS.Api/PDS.Api.csproj`, `src/PDS.Api/appsettings.Development.json` (one `Documents` block), `Directory.Packages.props` (one line), `infra/PDS.Infra/Program.cs`, `web/src/routes.tsx` (not needed), and `web/src/features/portfolio/ProjectDetailPage.tsx`. `infra/PDS.Infra/AppStack.cs` gets a small additive block (Task 3).
- **Don't modify `PDS.Shared` or `web/src/shared`**, apart from the regenerated `openapi.json` and `schema.d.ts`.
- **File bytes never pass through the API in S3 mode.** Only the Development-only `local-files` endpoints read or write bodies.
- **Policies:**
  - reads (including download links) need CanRead
  - uploads, edits and new versions need CanWrite
  - delete needs CanAdminister and is a **soft** delete
- **Limits:**
  - `DocumentRules`: title ≤ 200, file name ≤ 255, ≤ 10 tags of 1–40 chars, notes ≤ 2000, link label ≤ 200
  - `Documents:MaxFileSizeBytes` defaults to 104857600 (100 MiB)
  - upload URL lifetime 15 min; download URL lifetime 5 min
- Commit messages are conventional (`feat(documents): ...`) and end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **Upload confirmation edge cases.** Each of these must return a 400 on the named field, never a 500:
   - an unknown or expired `uploadId`
   - an `uploadId` issued for another project
   - confirming the same upload twice
   - an object larger than the max (the pending object is deleted)
   - a zero-byte object
   - a confirm `fileName` whose extension maps to a different content type than the stored object

   In S3 mode, `HeadObject` on a missing key returns **403** (the role has no `ListBucket`). Treat 403 and 404 alike.
2. **File type and name handling:**
   - `FILE.PDF` and `x.Pdf` are allowed.
   - `noextension`, `a.exe`, `a.html` and `a.svg` → 400 on `fileName`.
   - `C:\fakepath\plan.pdf` and `../../x.pdf` are reduced to their final segment.
   - The content type sent to S3 comes from the server, never the browser.
3. **Concurrency.** A new version or an edit with a stale `version` → 409. Two concurrent new versions can't both claim version n+1: the transaction condition makes the loser get a 409. The version check happens **before** the copy, so a stale request leaves no orphan object.
4. **Deleted and archived.**
   - A deleted document is absent from the list, and its get, download, edit, new version and delete all return 404.
   - An archived project lists and downloads, but rejects upload-URL requests, creates, edits and new versions with a 400 on `projectId`.
   - Delete is Admin-only (a Manager gets 403).
5. **Local storage safety:**
   - The `local-files` endpoints exist only when `Documents:Storage=Local`, and startup fails if `Local` is used outside Development.
   - A bad or expired signature returns 403.
   - A key that resolves outside the root returns 403.
   - The `PUT` lifts Kestrel's 30 MB body limit, so a 50 MB file works locally.

---

## File structure

```
src/Modules/PDS.Documents/
  PDS.Documents.csproj
  DocumentsModule.cs                    public: Tables, AddDocumentsModule(), MapDocumentsEndpoints()
  Contracts/DocumentId.cs
  Domain/AuditStamp.cs, LinkedModule.cs, DocumentLink.cs, DocumentFields.cs, DocumentVersion.cs, Document.cs, DocumentRules.cs
  Data/DocumentsTable.cs, DocumentItemMapper.cs, VersionItemMapper.cs, DocumentStore.cs
  Storage/DocumentsOptions.cs, IFileStorage.cs, FileTypes.cs, StorageKeys.cs, ContentDispositionHeader.cs,
          S3FileStorage.cs, LocalFileStorage.cs, LocalFileEndpoints.cs
  Features/ProjectLookup.cs, DocumentsProblems.cs, GetSettings.cs, RequestUpload.cs,
           DocumentDtos.cs, DocumentValidators.cs, UploadConfirmation.cs,
           ListDocuments.cs, GetDocument.cs, CreateDocument.cs, AddVersion.cs, UpdateDocument.cs,
           DeleteDocument.cs, DownloadVersion.cs
infra/PDS.Infra/DocumentsBucket.cs
web/src/features/documents/
  types.ts, api.ts, format.ts, documentFormModel.ts,
  ProjectDocumentsSection.tsx, UploadDocumentModal.tsx, DocumentMetadataFields.tsx,
  LinkedRecordSelect.tsx, EditDocumentModal.tsx, NewVersionModal.tsx, VersionsModal.tsx, DeleteDocumentModal.tsx
```

---

### Task 1: Documents backend — module scaffold, file storage (S3 + local), settings and upload-URL endpoints, wiring

**Files:**
- Create: `src/Modules/PDS.Documents/PDS.Documents.csproj`
- Create: `src/Modules/PDS.Documents/DocumentsModule.cs`
- Create: `src/Modules/PDS.Documents/Contracts/DocumentId.cs`
- Create: `src/Modules/PDS.Documents/Data/DocumentsTable.cs`
- Create: `src/Modules/PDS.Documents/Storage/*` (all files listed above)
- Create: `src/Modules/PDS.Documents/Features/ProjectLookup.cs`, `DocumentsProblems.cs`, `GetSettings.cs`, `RequestUpload.cs`
- Modify (one line each): `Directory.Packages.props`, `PDS.slnx`, `src/PDS.Api/PDS.Api.csproj`, `src/PDS.Api/Program.cs` (two lines), `infra/PDS.Infra/Program.cs`, `src/PDS.Api/appsettings.Development.json` (a `Documents` block)

**Interfaces:**
- Produces:
  - `DocumentId` (`New()`)
  - `DocumentsTable.Definition` / `LogicalName = "documents"`
  - `DocumentsOptions` (`Storage`, `BucketName`, `LocalPath`, `MaxFileSizeBytes`, `Categories`, `EffectiveCategories`)
  - `IFileStorage` and `StoredObject`
  - `FileTypes.ContentTypeFor(string)`, `FileTypes.CleanFileName(string)`, `FileTypes.Extensions`
  - `StorageKeys.Pending(ProjectId, Guid)`, `StorageKeys.File(ProjectId, DocumentId, Guid)`
  - `ProjectLookup.FindAsync`, `DocumentsProblems.*`
  - endpoints `GET /api/documents/settings` and `POST /api/documents/projects/{projectId}/documents/uploads`

- [ ] **Step 1: Package and project.** Add `<PackageVersion Include="AWSSDK.S3" Version="4.0.103.4" />` to `Directory.Packages.props`, keeping alphabetical order after `AWSSDK.DynamoDBv2`. Then create the csproj:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="AWSSDK.S3" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../PDS.Shared/PDS.Shared.csproj" />
    <!-- Only PDS.Portfolio.Contracts types may be used (module rule). -->
    <ProjectReference Include="../PDS.Portfolio/PDS.Portfolio.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Contracts, table, options, keys, file types.**

```csharp
// Contracts/DocumentId.cs
namespace PDS.Documents.Contracts;

public readonly record struct DocumentId(Guid Value)
{
    public static DocumentId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
```

```csharp
// Data/DocumentsTable.cs
using PDS.Shared.Data;

namespace PDS.Documents.Data;

internal static class DocumentsTable
{
    public const string LogicalName = "documents";

    public static readonly TableDefinition Definition = new(LogicalName, []);
}
```

```csharp
// Storage/DocumentsOptions.cs
namespace PDS.Documents.Storage;

internal enum StorageMode
{
    S3,
    Local,
}

/// <summary>Bound from the "Documents" configuration section.</summary>
internal sealed class DocumentsOptions
{
    public const string Section = "Documents";

    public static readonly IReadOnlyList<string> DefaultCategories =
        ["Contract", "Consent", "Drawing", "Report", "Invoice", "Photo", "Correspondence", "Other"];

    public StorageMode Storage { get; set; } = StorageMode.S3;

    /// <summary>S3 bucket for files (set by CDK as Documents__BucketName).</summary>
    public string? BucketName { get; set; }

    /// <summary>Local mode only. Defaults to &lt;temp&gt;/pds-documents.</summary>
    public string? LocalPath { get; set; }

    public long MaxFileSizeBytes { get; set; } = 100L * 1024 * 1024;

    /// <summary>Empty means the defaults. (Binding appends to a non-empty initial list, so the default lives elsewhere.)</summary>
    public List<string> Categories { get; set; } = [];

    public IReadOnlyList<string> EffectiveCategories =>
        Categories.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            is { Count: > 0 } configured ? configured : DefaultCategories;
}
```

```csharp
// Storage/StorageKeys.cs
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
```

```csharp
// Storage/FileTypes.cs
namespace PDS.Documents.Storage;

/// <summary>Extension allowlist. The server picks the content type; the browser's guess is never used.</summary>
internal static class FileTypes
{
    private static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pdf"] = "application/pdf",
        ["jpg"] = "image/jpeg",
        ["jpeg"] = "image/jpeg",
        ["png"] = "image/png",
        ["gif"] = "image/gif",
        ["webp"] = "image/webp",
        ["heic"] = "image/heic",
        ["tif"] = "image/tiff",
        ["tiff"] = "image/tiff",
        ["doc"] = "application/msword",
        ["docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ["xls"] = "application/vnd.ms-excel",
        ["xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ["ppt"] = "application/vnd.ms-powerpoint",
        ["pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ["csv"] = "text/csv",
        ["txt"] = "text/plain",
        ["rtf"] = "application/rtf",
        ["msg"] = "application/vnd.ms-outlook",
        ["eml"] = "message/rfc822",
        ["dwg"] = "image/vnd.dwg",
        ["dxf"] = "image/vnd.dxf",
        ["zip"] = "application/zip",
    };

    public static IReadOnlyList<string> Extensions { get; } = ByExtension.Keys.Order(StringComparer.Ordinal).ToList();

    /// <summary>The final path segment, trimmed. Handles both / and \ (browsers may send C:\fakepath\x.pdf).</summary>
    public static string CleanFileName(string? fileName) =>
        Path.GetFileName((fileName ?? string.Empty).Replace('\\', '/')).Trim();

    /// <returns>The canonical content type, or null when the extension is not allowed.</returns>
    public static string? ContentTypeFor(string? fileName)
    {
        var extension = Path.GetExtension(CleanFileName(fileName)).TrimStart('.');
        return extension.Length > 0 && ByExtension.TryGetValue(extension, out var type) ? type : null;
    }

    public static string AllowedList => string.Join(", ", Extensions);
}
```

- [ ] **Step 3: Storage interface, Content-Disposition, S3 implementation.**

```csharp
// Storage/IFileStorage.cs
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
```

```csharp
// Storage/ContentDispositionHeader.cs
using System.Text;

namespace PDS.Documents.Storage;

internal static class ContentDispositionHeader
{
    /// <summary>attachment; filename="ascii-fallback"; filename*=UTF-8''percent-encoded (RFC 6266).</summary>
    public static string Attachment(string fileName)
    {
        var ascii = new StringBuilder(fileName.Length);
        foreach (var c in fileName)
            ascii.Append(c is >= ' ' and <= '~' and not '"' and not '\\' ? c : '_');
        return $"attachment; filename=\"{ascii}\"; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
    }
}
```

```csharp
// Storage/S3FileStorage.cs
using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PDS.Documents.Storage;

internal sealed class S3FileStorage(IAmazonS3 s3, IOptions<DocumentsOptions> options, ILogger<S3FileStorage> logger)
    : IFileStorage
{
    private string Bucket => options.Value.BucketName is { Length: > 0 } bucket
        ? bucket
        : throw new InvalidOperationException("Documents:BucketName is not configured.");

    public Task<string> CreateUploadUrlAsync(string key, string contentType, DateTimeOffset expiresAt, CancellationToken ct) =>
        s3.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = Bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = expiresAt.UtcDateTime,
        });

    public Task<string> CreateDownloadUrlAsync(
        string key, string fileName, string contentType, DateTimeOffset expiresAt, CancellationToken ct) =>
        s3.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = Bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = expiresAt.UtcDateTime,
            ResponseHeaderOverrides = new ResponseHeaderOverrides
            {
                ContentDisposition = ContentDispositionHeader.Attachment(fileName),
                ContentType = contentType,
            },
        });

    public async Task<StoredObject?> HeadAsync(string key, CancellationToken ct)
    {
        try
        {
            var head = await s3.GetObjectMetadataAsync(new GetObjectMetadataRequest { BucketName = Bucket, Key = key }, ct);
            return new StoredObject(head.ContentLength, head.Headers.ContentType);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
        {
            // Without s3:ListBucket, S3 reports a missing key as 403. Log so a real permission fault is visible.
            if (ex.StatusCode == HttpStatusCode.Forbidden)
                logger.LogInformation("HeadObject on {Key} returned 403; treating it as missing.", key);
            return null;
        }
    }

    public Task CopyAsync(string sourceKey, string destinationKey, CancellationToken ct) =>
        s3.CopyObjectAsync(new CopyObjectRequest
        {
            SourceBucket = Bucket,
            SourceKey = sourceKey,
            DestinationBucket = Bucket,
            DestinationKey = destinationKey,
            MetadataDirective = S3MetadataDirective.COPY,
        }, ct);

    public Task DeleteAsync(string key, CancellationToken ct) =>
        s3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = Bucket, Key = key }, ct);
}
```

*SDK v4 note:* some value properties became nullable in v4 (for example `ContentLength` may be `long?`). If the compiler complains, use `head.ContentLength ?? 0`, and check `GetPreSignedURLAsync`'s exact signature in the package. Fix to compile with 0 warnings without changing behaviour.

- [ ] **Step 4: Local storage (Development only).**

```csharp
// Storage/LocalFileStorage.cs
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace PDS.Documents.Storage;

/// <summary>
/// Development stand-in for S3: files under a local directory, "presigned" URLs pointing at the anonymous
/// /api/documents/local-files endpoints, guarded by an HMAC signature (random key per process) and an expiry.
/// </summary>
internal sealed class LocalFileStorage(IOptions<DocumentsOptions> options, IHttpContextAccessor http) : IFileStorage
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
        return Task.FromResult($"{BaseUrl()}{RoutePrefix}/{key}?expires={expires}&sig={sig}");
    }

    public Task<string> CreateDownloadUrlAsync(
        string key, string fileName, string contentType, DateTimeOffset expiresAt, CancellationToken ct)
    {
        var expires = expiresAt.ToUnixTimeSeconds();
        var sig = Sign("GET", key, expires, contentType, fileName);
        return Task.FromResult(
            $"{BaseUrl()}{RoutePrefix}/{key}?expires={expires}&type={Uri.EscapeDataString(contentType)}" +
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

    /// <summary>The caller's scheme and host, so URLs work through the Vite proxy too.</summary>
    private string BaseUrl() =>
        http.HttpContext?.Request is { } request ? $"{request.Scheme}://{request.Host}" : string.Empty;
}
```

```csharp
// Storage/LocalFileEndpoints.cs
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;

namespace PDS.Documents.Storage;

/// <summary>Development-only stand-ins for S3 presigned PUT/GET. Mapped only when Documents:Storage=Local.</summary>
internal static class LocalFileEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(LocalFileStorage.RoutePrefix).AllowAnonymous().ExcludeFromDescription();
        group.MapPut("/{**key}", Put);
        group.MapGet("/{**key}", Get);
    }

    private static async Task<IResult> Put(
        string key, long expires, string? sig, HttpContext context, LocalFileStorage storage, TimeProvider clock)
    {
        var contentType = context.Request.ContentType ?? string.Empty;
        if (!storage.Verify("PUT", key, expires, contentType, string.Empty, sig, clock.GetUtcNow()) || storage.PathFor(key) is not { } path)
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        // S3 accepts up to 5 GB per PUT; lift Kestrel's 30 MB default so local behaves the same.
        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            limit.MaxRequestBodySize = null;
        await storage.WriteAsync(path, context.Request.Body, contentType, context.RequestAborted);
        return Results.Ok();
    }

    private static IResult Get(
        string key, long expires, string? type, string? name, string? sig, LocalFileStorage storage, TimeProvider clock)
    {
        if (type is null || name is null
            || !storage.Verify("GET", key, expires, type, name, sig, clock.GetUtcNow())
            || storage.PathFor(key) is not { } path || !File.Exists(path))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        return Results.File(path, type, name);
    }
}
```

- [ ] **Step 5: Problems, project lookup, settings and upload-URL slices.**

```csharp
// Features/ProjectLookup.cs  (same as Consents)
using PDS.Portfolio.Contracts;

namespace PDS.Documents.Features;

internal static class ProjectLookup
{
    public static async Task<ProjectSummary?> FindAsync(IPortfolioQueries portfolio, Guid projectId, CancellationToken ct) =>
        (await portfolio.GetSummaries([new ProjectId(projectId)], ct)).FirstOrDefault();
}
```

```csharp
// Features/DocumentsProblems.cs
using Microsoft.AspNetCore.Http.HttpResults;
using PDS.Documents.Storage;
using PDS.Shared.Web;

namespace PDS.Documents.Features;

internal static class DocumentsProblems
{
    public static ValidationProblem ProjectArchived() =>
        Problems.Field("projectId", "Documents can't be uploaded or changed on an archived project.");

    public static ValidationProblem FileTypeNotAllowed() =>
        Problems.Field("fileName", $"This file type isn't allowed. Allowed: {FileTypes.AllowedList}.");

    public static ValidationProblem UploadMissing() =>
        Problems.Field("uploadId", "The upload wasn't found or has expired. Upload the file again.");

    public static ValidationProblem TooLarge(long max) =>
        Problems.Field("sizeBytes", $"The file is larger than the {max / (1024 * 1024)} MB limit.");

    public static ValidationProblem Empty() => Problems.Field("sizeBytes", "The file is empty.");

    public static ValidationProblem TypeMismatch() =>
        Problems.Field("fileName", "The uploaded file doesn't match its file name. Upload it again.");

    public static ProblemHttpResult VersionConflict() => Problems.Conflict(
        "This document was changed by someone else.",
        "Reload to see the latest changes, then apply your edits again.");
}
```

```csharp
// Features/GetSettings.cs
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Documents.Storage;

namespace PDS.Documents.Features;

internal sealed record DocumentSettings(IReadOnlyList<string> Categories, long MaxFileSizeBytes, IReadOnlyList<string> AllowedExtensions);

internal static class GetSettings
{
    public static void Map(RouteGroupBuilder group) => group.MapGet("/settings", Handle).WithName("GetDocumentSettings");

    internal static Ok<DocumentSettings> Handle(IOptions<DocumentsOptions> options) =>
        TypedResults.Ok(new DocumentSettings(options.Value.EffectiveCategories, options.Value.MaxFileSizeBytes, FileTypes.Extensions));
}
```

```csharp
// Features/RequestUpload.cs
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Documents.Storage;
using PDS.Portfolio.Contracts;
using PDS.Shared.Security;
using PDS.Shared.Web;

namespace PDS.Documents.Features;

internal sealed record RequestUploadRequest(string FileName, long SizeBytes);

internal sealed record UploadTicket(Guid UploadId, string UploadUrl, string ContentType, DateTimeOffset ExpiresAt);

internal sealed class RequestUploadValidator : AbstractValidator<RequestUploadRequest>
{
    public RequestUploadValidator(IOptions<DocumentsOptions> options)
    {
        RuleFor(x => x.FileName).Cascade(CascadeMode.Stop)
            .Must(n => FileTypes.CleanFileName(n).Length > 0).WithMessage("Choose a file.")
            .Must(n => FileTypes.CleanFileName(n).Length <= Domain.DocumentRules.FileNameMaxLength)
            .WithMessage($"Use a file name of {Domain.DocumentRules.FileNameMaxLength} characters or fewer.")
            .Must(n => FileTypes.ContentTypeFor(n) is not null)
            .WithMessage(_ => $"This file type isn't allowed. Allowed: {FileTypes.AllowedList}.");
        RuleFor(x => x.SizeBytes).Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage("The file is empty.")
            .LessThanOrEqualTo(options.Value.MaxFileSizeBytes)
            .WithMessage($"The file is larger than the {options.Value.MaxFileSizeBytes / (1024 * 1024)} MB limit.");
    }
}

internal static class RequestUpload
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/uploads", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<RequestUploadRequest>()
            .WithName("RequestDocumentUpload");

    internal static async Task<Results<Ok<UploadTicket>, NotFound, ValidationProblem>> Handle(
        Guid projectId, RequestUploadRequest request, IPortfolioQueries portfolio, IFileStorage storage,
        TimeProvider clock, CancellationToken ct)
    {
        var project = await ProjectLookup.FindAsync(portfolio, projectId, ct);
        if (project is null)
            return TypedResults.NotFound();
        if (project.IsArchived)
            return DocumentsProblems.ProjectArchived();

        var contentType = FileTypes.ContentTypeFor(request.FileName)!;
        var uploadId = Guid.CreateVersion7();
        var expiresAt = clock.GetUtcNow().Add(Lifetime);
        var url = await storage.CreateUploadUrlAsync(
            StorageKeys.Pending(new ProjectId(projectId), uploadId), contentType, expiresAt, ct);
        return TypedResults.Ok(new UploadTicket(uploadId, url, contentType, expiresAt));
    }
}
```

`RequestUploadValidator` refers to `Domain.DocumentRules.FileNameMaxLength`. In this task, create `Domain/DocumentRules.cs` containing only the length constants. Task 2 extends it:

```csharp
namespace PDS.Documents.Domain;

internal static class DocumentRules
{
    public const int TitleMaxLength = 200;
    public const int FileNameMaxLength = 255;
    public const int MaxTags = 10;
    public const int TagMaxLength = 40;
    public const int NotesMaxLength = 2000;
    public const int LinkLabelMaxLength = 200;
}
```

- [ ] **Step 6: Module class.**

```csharp
// DocumentsModule.cs
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PDS.Documents.Data;
using PDS.Documents.Features;
using PDS.Documents.Storage;
using PDS.Shared.Data;
using PDS.Shared.Security;

namespace PDS.Documents;

public static class DocumentsModule
{
    /// <summary>Tables this module owns. The host creates them locally; CDK creates them in AWS.</summary>
    public static IReadOnlyList<TableDefinition> Tables { get; } = [DocumentsTable.Definition];

    public static IServiceCollection AddDocumentsModule(this IServiceCollection services)
    {
        services.AddSingleton(DocumentsTable.Definition);
        services.AddOptions<DocumentsOptions>()
            .BindConfiguration(DocumentsOptions.Section)
            .Validate<IHostEnvironment>(
                (options, env) => options.Storage != StorageMode.Local || env.IsDevelopment(),
                "Documents:Storage=Local is only allowed when ASPNETCORE_ENVIRONMENT is Development.")
            .Validate(options => options.MaxFileSizeBytes > 0, "Documents:MaxFileSizeBytes must be positive.")
            .ValidateOnStart();
        services.AddHttpContextAccessor();

        // Created lazily: Local mode and build-time OpenAPI generation never construct the S3 client.
        services.AddSingleton<IAmazonS3>(_ =>
        {
            try
            {
                return new AmazonS3Client();
            }
            catch (AmazonClientException)
            {
                return new AmazonS3Client(RegionEndpoint.APSoutheast2);
            }
        });
        services.AddSingleton<S3FileStorage>();
        services.AddSingleton<LocalFileStorage>();
        services.AddSingleton<IFileStorage>(sp =>
            sp.GetRequiredService<IOptions<DocumentsOptions>>().Value.Storage == StorageMode.Local
                ? sp.GetRequiredService<LocalFileStorage>()
                : sp.GetRequiredService<S3FileStorage>());

        services.AddValidatorsFromAssemblyContaining<RequestUploadValidator>(includeInternalTypes: true);
        return services;
    }

    public static IEndpointRouteBuilder MapDocumentsEndpoints(this IEndpointRouteBuilder app)
    {
        var root = app.MapGroup("/api/documents").WithTags("Documents").RequireAuthorization(Policies.CanRead);
        GetSettings.Map(root);

        var group = app.MapGroup("/api/documents/projects/{projectId:guid}/documents")
            .WithTags("Documents")
            .RequireAuthorization(Policies.CanRead);
        RequestUpload.Map(group);

        if (app.ServiceProvider.GetRequiredService<IOptions<DocumentsOptions>>().Value.Storage == StorageMode.Local)
            LocalFileEndpoints.Map(app);
        return app;
    }
}
```

(Task 2 adds the `DocumentStore` registration and the other `Map` calls.) The `Microsoft.AspNetCore.Http` using may be unused; remove any unused usings so the build has 0 warnings.

- [ ] **Step 7: Wiring.** Make each of these edits as one line:
  - `PDS.slnx`: `<Project Path="src/Modules/PDS.Documents/PDS.Documents.csproj" />`, in alphabetical order inside `/src/Modules/`.
  - `src/PDS.Api/PDS.Api.csproj`: a `ProjectReference` to `../Modules/PDS.Documents/PDS.Documents.csproj`.
  - `Program.cs`: `using PDS.Documents;`, `builder.Services.AddDocumentsModule();` and `app.MapDocumentsEndpoints();` (after Sales).
  - `infra/PDS.Infra/Program.cs`: `using PDS.Documents;` and `.. DocumentsModule.Tables` appended to the list.
  - `appsettings.Development.json`: add `"Documents": { "Storage": "Local" }`.

- [ ] **Step 8: Verify.**
  - `dotnet format PDS.slnx && dotnet build PDS.slnx`: 0 warnings, and `openapi.json` regenerates with the two new operations.
  - Start the API on 5086 (Development) and run:

```bash
curl -s localhost:5086/api/documents/settings
PID=$(curl -s 'localhost:5086/api/portfolio/projects?pageSize=1' | python3 -c 'import json,sys;print(json.load(sys.stdin)["items"][0]["id"])')
# (create a project first with POST /api/portfolio/projects if none exists)
curl -s -XPOST localhost:5086/api/documents/projects/$PID/documents/uploads -H 'content-type: application/json' -d '{"fileName":"C:\\fakepath\\Plan.PDF","sizeBytes":12}'
# → uploadId, uploadUrl (http://localhost:5086/api/documents/local-files/pending/...), contentType application/pdf
curl -s -o /dev/null -w '%{http_code}\n' -XPUT "<uploadUrl>" -H 'content-type: application/pdf' --data-binary 'hello world!'   # 200
curl -s -o /dev/null -w '%{http_code}\n' -XPUT "<uploadUrl>" -H 'content-type: text/plain' --data-binary 'x'               # 403
curl -s -XPOST .../uploads -d '{"fileName":"a.exe","sizeBytes":1}' -H 'content-type: application/json'   # 400 fileName
curl -s -XPOST .../uploads -d '{"fileName":"a.pdf","sizeBytes":0}' -H 'content-type: application/json'   # 400 sizeBytes
```

- [ ] **Step 9: Commit:** `feat(documents): module scaffold, S3/local file storage, upload URLs`.

---

### Task 2: Documents register backend — domain, table, confirm/create, list, get, edit, new version, delete, download

**Files:**
- Create: `Domain/AuditStamp.cs`, `LinkedModule.cs`, `DocumentLink.cs`, `DocumentFields.cs`, `DocumentVersion.cs`, `Document.cs`; extend `Domain/DocumentRules.cs`
- Create: `Data/DocumentItemMapper.cs`, `VersionItemMapper.cs`, `DocumentStore.cs`
- Create: `Features/DocumentDtos.cs`, `DocumentValidators.cs`, `UploadConfirmation.cs`, `ListDocuments.cs`, `GetDocument.cs`, `CreateDocument.cs`, `AddVersion.cs`, `UpdateDocument.cs`, `DeleteDocument.cs`, `DownloadVersion.cs`
- Modify: `DocumentsModule.cs`. Register `DocumentStore` and map the new slices.

**Interfaces:**
- Consumes: everything Task 1 produced.
- Produces (HTTP, used by the web tasks):
  - `DocumentDetails` JSON: `id, projectId, title, category, link {module, recordId, label} | null, tags[], notes, currentVersion, fileName, contentType, sizeBytes, uploadedAt, uploadedBy, versions[{number, fileName, contentType, sizeBytes, uploadedAt, uploadedBy}], createdAt, createdBy, updatedAt, updatedBy, version`
  - `DownloadLink` JSON: `{url, expiresAt}`
  - `LinkedModule` values: `Consent | Contract | Risk`
  - Operation names: `ListDocuments`, `GetDocument`, `CreateDocument`, `AddDocumentVersion`, `UpdateDocument`, `DeleteDocument`, `DownloadDocumentVersion`

- [ ] **Step 1: Domain.**

```csharp
// Domain/AuditStamp.cs
namespace PDS.Documents.Domain;

internal readonly record struct AuditStamp(DateTimeOffset At, string By);
```

```csharp
// Domain/LinkedModule.cs
namespace PDS.Documents.Domain;

/// <summary>Which module a linked record belongs to. A typed reference only: existence is not checked (spec §1).</summary>
internal enum LinkedModule
{
    Consent,
    Contract,
    Risk,
}
```

```csharp
// Domain/DocumentLink.cs
namespace PDS.Documents.Domain;

/// <summary>Soft link to another module's record, with a label snapshot for display.</summary>
internal sealed record DocumentLink(LinkedModule? Module, Guid RecordId, string Label);
```

`Module` is nullable so that a missing value is a 400, not a default. After validation it's always set.

```csharp
// Domain/DocumentFields.cs
namespace PDS.Documents.Domain;

internal sealed record DocumentFields(string Title, string Category, DocumentLink? Link, IReadOnlyList<string> Tags, string? Notes);
```

```csharp
// Domain/DocumentVersion.cs
namespace PDS.Documents.Domain;

/// <summary>One uploaded file. Immutable once written.</summary>
internal sealed record DocumentVersion(
    int Number, string FileName, string ContentType, long SizeBytes, string StorageKey, AuditStamp Uploaded);
```

Extend `DocumentRules`:

```csharp
    public static DocumentFields Normalise(DocumentFields f, IReadOnlyList<string> categories) => f with
    {
        Title = f.Title.Trim(),
        // Store the configured spelling of the category.
        Category = categories.FirstOrDefault(c => string.Equals(c, f.Category.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? f.Category.Trim(),
        Link = f.Link is { } link ? link with { Label = link.Label.Trim() } : null,
        Tags = NormaliseTags(f.Tags),
        Notes = string.IsNullOrWhiteSpace(f.Notes) ? null : f.Notes.Trim(),
    };

    /// <summary>Trimmed, blanks dropped, de-duplicated case-insensitively (first spelling kept).</summary>
    public static IReadOnlyList<string> NormaliseTags(IEnumerable<string>? tags) =>
        (tags ?? []).Select(t => t?.Trim() ?? string.Empty).Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
```

```csharp
// Domain/Document.cs
using PDS.Documents.Contracts;
using PDS.Portfolio.Contracts;

namespace PDS.Documents.Domain;

internal sealed class Document
{
    private Document(
        DocumentId id, ProjectId projectId, DocumentFields fields, DocumentVersion current, AuditStamp? deleted,
        AuditStamp created, AuditStamp updated, long version)
    {
        Id = id;
        ProjectId = projectId;
        Fields = fields;
        CurrentVersion = current;
        Deleted = deleted;
        Created = created;
        Updated = updated;
        Version = version;
    }

    public DocumentId Id { get; }

    public ProjectId ProjectId { get; }

    public DocumentFields Fields { get; private set; }

    public DocumentVersion CurrentVersion { get; private set; }

    public AuditStamp? Deleted { get; private set; }

    public bool IsDeleted => Deleted is not null;

    public AuditStamp Created { get; }

    public AuditStamp Updated { get; private set; }

    public long Version { get; private set; }

    /// <summary>Fields must already be validated and normalised.</summary>
    public static Document Create(DocumentId id, ProjectId projectId, DocumentFields fields, DocumentVersion first, AuditStamp stamp) =>
        first.Number == 1
            ? new Document(id, projectId, fields, first, null, stamp, stamp, 1)
            : throw new ArgumentException("The first version must be number 1.", nameof(first));

    public static Document Rehydrate(
        DocumentId id, ProjectId projectId, DocumentFields fields, DocumentVersion current, AuditStamp? deleted,
        AuditStamp created, AuditStamp updated, long version) =>
        new(id, projectId, fields, current, deleted, created, updated, version);

    public void Update(DocumentFields fields, AuditStamp stamp)
    {
        Fields = fields;
        Touch(stamp);
    }

    public void AddVersion(DocumentVersion next, AuditStamp stamp)
    {
        if (next.Number != CurrentVersion.Number + 1)
            throw new ArgumentException("Versions are numbered consecutively.", nameof(next));
        CurrentVersion = next;
        Touch(stamp);
    }

    public void MarkDeleted(AuditStamp stamp)
    {
        Deleted = stamp;
        Touch(stamp);
    }

    private void Touch(AuditStamp stamp)
    {
        Updated = stamp;
        Version++;
    }
}
```

- [ ] **Step 2: Mappers.** `itemType` is the discriminator. The document item stores the current version inline under the **same attribute names** the version item uses, so `VersionItemMapper` can read both.

```csharp
// Data/VersionItemMapper.cs
using System.Globalization;
using Amazon.DynamoDBv2.Model;
using PDS.Documents.Contracts;
using PDS.Documents.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared.Data;

namespace PDS.Documents.Data;

/// <summary>Version item: pk=PROJECT#projectId, sk=DOC#documentId#V#000001 (sorts under its document).</summary>
internal static class VersionItemMapper
{
    public const string ItemType = "Version";

    public static string SortKey(DocumentId documentId, int number) =>
        $"{DocumentItemMapper.SortKey(documentId)}#V#{number.ToString("D6", CultureInfo.InvariantCulture)}";

    public static Dictionary<string, AttributeValue> Key(ProjectId projectId, DocumentId documentId, int number) => new()
    {
        [TableKeys.PartitionKey] = Attr.S(DocumentItemMapper.PartitionKey(projectId)),
        [TableKeys.SortKey] = Attr.S(SortKey(documentId, number)),
    };

    public static Dictionary<string, AttributeValue> ToItem(ProjectId projectId, DocumentId documentId, DocumentVersion v)
    {
        var item = Key(projectId, documentId, v.Number);
        item[DocumentItemMapper.ItemTypeAttr] = Attr.S(ItemType);
        item["documentId"] = Attr.S(documentId.Value.ToString());
        WriteVersion(item, v);
        return item;
    }

    /// <summary>Shared by the version item and the document item's inline current version.</summary>
    public static void WriteVersion(Dictionary<string, AttributeValue> item, DocumentVersion v)
    {
        item["versionNumber"] = Attr.N(v.Number);
        item["fileName"] = Attr.S(v.FileName);
        item["contentType"] = Attr.S(v.ContentType);
        item["sizeBytes"] = Attr.N(v.SizeBytes);
        item["storageKey"] = Attr.S(v.StorageKey);
        item["uploadedAt"] = Attr.Timestamp(v.Uploaded.At);
        item["uploadedBy"] = Attr.S(v.Uploaded.By);
    }

    public static DocumentVersion ReadVersion(IReadOnlyDictionary<string, AttributeValue> item) => new(
        (int)Attr.GetLong(item, "versionNumber"),
        Attr.GetString(item, "fileName"),
        Attr.GetString(item, "contentType"),
        Attr.GetLong(item, "sizeBytes"),
        Attr.GetString(item, "storageKey"),
        new AuditStamp(Attr.GetTimestamp(item, "uploadedAt"), Attr.GetString(item, "uploadedBy")));

    public static DocumentId DocumentIdOf(IReadOnlyDictionary<string, AttributeValue> item) =>
        new(Guid.Parse(Attr.GetString(item, "documentId")));
}
```

```csharp
// Data/DocumentItemMapper.cs
using Amazon.DynamoDBv2.Model;
using PDS.Documents.Contracts;
using PDS.Documents.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared.Data;

namespace PDS.Documents.Data;

/// <summary>Document item: pk=PROJECT#projectId, sk=DOC#documentId. Optional attributes are omitted when empty.</summary>
internal static class DocumentItemMapper
{
    public const string DocumentPrefix = "DOC#";
    public const string ItemTypeAttr = "itemType";
    public const string ItemType = "Document";

    public static string PartitionKey(ProjectId projectId) => $"PROJECT#{projectId.Value}";

    public static string SortKey(DocumentId documentId) => $"{DocumentPrefix}{documentId.Value}";

    public static Dictionary<string, AttributeValue> Key(ProjectId projectId, DocumentId documentId) => new()
    {
        [TableKeys.PartitionKey] = Attr.S(PartitionKey(projectId)),
        [TableKeys.SortKey] = Attr.S(SortKey(documentId)),
    };

    public static Dictionary<string, AttributeValue> ToItem(Document d)
    {
        var f = d.Fields;
        var item = Key(d.ProjectId, d.Id);
        item[ItemTypeAttr] = Attr.S(ItemType);
        item["id"] = Attr.S(d.Id.Value.ToString());
        item["projectId"] = Attr.S(d.ProjectId.Value.ToString());
        item["title"] = Attr.S(f.Title);
        item["category"] = Attr.S(f.Category);
        if (f.Link is { } link)
        {
            item["link"] = Attr.Map(new Dictionary<string, AttributeValue>
            {
                ["module"] = Attr.S(link.Module!.Value.ToString()),
                ["recordId"] = Attr.S(link.RecordId.ToString()),
                ["label"] = Attr.S(link.Label),
            });
        }

        if (f.Tags.Count > 0)
            item["tags"] = new AttributeValue { L = f.Tags.Select(Attr.S).ToList() };
        Attr.SetIfPresent(item, "notes", f.Notes);
        VersionItemMapper.WriteVersion(item, d.CurrentVersion);
        if (d.Deleted is { } deleted)
        {
            item["deletedAt"] = Attr.Timestamp(deleted.At);
            item["deletedBy"] = Attr.S(deleted.By);
        }

        item["createdAt"] = Attr.Timestamp(d.Created.At);
        item["createdBy"] = Attr.S(d.Created.By);
        item["updatedAt"] = Attr.Timestamp(d.Updated.At);
        item["updatedBy"] = Attr.S(d.Updated.By);
        item["version"] = Attr.N(d.Version);
        return item;
    }

    public static Document FromItem(IReadOnlyDictionary<string, AttributeValue> item)
    {
        DocumentLink? link = null;
        if (item.TryGetValue("link", out var linkValue) && linkValue.M is { } m)
        {
            link = new DocumentLink(
                Enum.Parse<LinkedModule>(Attr.GetString(m, "module")),
                Guid.Parse(Attr.GetString(m, "recordId")),
                Attr.GetString(m, "label"));
        }

        IReadOnlyList<string> tags = item.TryGetValue("tags", out var tagsValue) && tagsValue.L is { } list
            ? list.Select(v => v.S).Where(s => s is not null).ToList()!
            : [];

        var fields = new DocumentFields(
            Attr.GetString(item, "title"), Attr.GetString(item, "category"), link, tags, Attr.GetStringOrNull(item, "notes"));

        AuditStamp? deleted = Attr.GetStringOrNull(item, "deletedAt") is not null
            ? new AuditStamp(Attr.GetTimestamp(item, "deletedAt"), Attr.GetString(item, "deletedBy"))
            : null;

        return Document.Rehydrate(
            new DocumentId(Guid.Parse(Attr.GetString(item, "id"))),
            new ProjectId(Guid.Parse(Attr.GetString(item, "projectId"))),
            fields,
            VersionItemMapper.ReadVersion(item),
            deleted,
            new AuditStamp(Attr.GetTimestamp(item, "createdAt"), Attr.GetString(item, "createdBy")),
            new AuditStamp(Attr.GetTimestamp(item, "updatedAt"), Attr.GetString(item, "updatedBy")),
            Attr.GetLong(item, "version"));
    }
}
```

- [ ] **Step 3: Store.**

```csharp
// Data/DocumentStore.cs
using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using PDS.Documents.Contracts;
using PDS.Documents.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared.Data;

namespace PDS.Documents.Data;

/// <summary>A document with all its versions, newest first.</summary>
internal sealed record DocumentRecord(Document Document, IReadOnlyList<DocumentVersion> Versions);

internal sealed class DocumentStore(IAmazonDynamoDB dynamo, TableNames names)
{
    private string Table => names.For(DocumentsTable.LogicalName);

    /// <summary>Includes deleted documents; callers filter.</summary>
    public async Task<IReadOnlyList<DocumentRecord>> ListForProjectAsync(ProjectId projectId, CancellationToken ct) =>
        Group(await QueryAsync(projectId, DocumentItemMapper.DocumentPrefix, ct));

    public async Task<DocumentRecord?> GetAsync(ProjectId projectId, DocumentId documentId, CancellationToken ct) =>
        Group(await QueryAsync(projectId, DocumentItemMapper.SortKey(documentId), ct)).FirstOrDefault();

    /// <summary>Puts the document and its version 1 atomically.</summary>
    public Task CreateAsync(Document document, CancellationToken ct) =>
        dynamo.TransactWriteItemsAsync(new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem { Put = new Put { TableName = Table, Item = DocumentItemMapper.ToItem(document), ConditionExpression = "attribute_not_exists(pk)" } },
                new TransactWriteItem { Put = NewVersionPut(document) },
            ],
        }, ct);

    /// <returns>False when the document's version changed or the version number is already taken.</returns>
    public async Task<bool> AddVersionAsync(Document document, long expectedVersion, CancellationToken ct)
    {
        try
        {
            await dynamo.TransactWriteItemsAsync(new TransactWriteItemsRequest
            {
                TransactItems =
                [
                    new TransactWriteItem { Put = VersionedPut(DocumentItemMapper.ToItem(document), expectedVersion) },
                    new TransactWriteItem { Put = NewVersionPut(document) },
                ],
            }, ct);
            return true;
        }
        catch (TransactionCanceledException)
        {
            return false;
        }
    }

    /// <returns>False when the stored version no longer equals <paramref name="expectedVersion"/>.</returns>
    public async Task<bool> UpdateAsync(Document document, long expectedVersion, CancellationToken ct)
    {
        var put = VersionedPut(DocumentItemMapper.ToItem(document), expectedVersion);
        try
        {
            await dynamo.PutItemAsync(new PutItemRequest
            {
                TableName = put.TableName,
                Item = put.Item,
                ConditionExpression = put.ConditionExpression,
                ExpressionAttributeNames = put.ExpressionAttributeNames,
                ExpressionAttributeValues = put.ExpressionAttributeValues,
            }, ct);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    private Put NewVersionPut(Document document) => new()
    {
        TableName = Table,
        Item = VersionItemMapper.ToItem(document.ProjectId, document.Id, document.CurrentVersion),
        ConditionExpression = "attribute_not_exists(pk)",
    };

    private Put VersionedPut(Dictionary<string, AttributeValue> item, long expectedVersion) => new()
    {
        TableName = Table,
        Item = item,
        ConditionExpression = "#version = :expected",
        ExpressionAttributeNames = new Dictionary<string, string> { ["#version"] = "version" },
        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
        {
            [":expected"] = new() { N = expectedVersion.ToString(CultureInfo.InvariantCulture) },
        },
    };

    private async Task<List<Dictionary<string, AttributeValue>>> QueryAsync(ProjectId projectId, string prefix, CancellationToken ct)
    {
        var items = new List<Dictionary<string, AttributeValue>>();
        Dictionary<string, AttributeValue>? start = null;
        do
        {
            var response = await dynamo.QueryAsync(new QueryRequest
            {
                TableName = Table,
                KeyConditionExpression = "pk = :pk AND begins_with(sk, :prefix)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":pk"] = Attr.S(DocumentItemMapper.PartitionKey(projectId)),
                    [":prefix"] = Attr.S(prefix),
                },
                ConsistentRead = true,
                ExclusiveStartKey = start,
            }, ct);
            items.AddRange(response.Items ?? []);
            start = response.LastEvaluatedKey is { Count: > 0 } next ? next : null;
        }
        while (start is not null);

        return items;
    }

    /// <summary>Splits by itemType; versions whose document item is missing are dropped (never throw).</summary>
    private static IReadOnlyList<DocumentRecord> Group(IEnumerable<Dictionary<string, AttributeValue>> items)
    {
        var documents = new List<Document>();
        var versions = new List<(DocumentId DocumentId, DocumentVersion Version)>();
        foreach (var item in items)
        {
            switch (Attr.GetStringOrNull(item, DocumentItemMapper.ItemTypeAttr))
            {
                case DocumentItemMapper.ItemType:
                    documents.Add(DocumentItemMapper.FromItem(item));
                    break;
                case VersionItemMapper.ItemType:
                    versions.Add((VersionItemMapper.DocumentIdOf(item), VersionItemMapper.ReadVersion(item)));
                    break;
            }
        }

        var byDocument = versions.ToLookup(v => v.DocumentId, v => v.Version);
        return documents
            .Select(d => new DocumentRecord(d, byDocument[d.Id].OrderByDescending(v => v.Number).ToList()))
            .ToList();
    }
}
```

The SDK's `Put.ExpressionAttributeNames` and `PutItemRequest` property types are both `Dictionary<,>`, so they assign directly. If the v4 types differ, build the `PutItemRequest` from scratch the way Consents does.

- [ ] **Step 4: DTOs and validators.**

```csharp
// Features/DocumentDtos.cs
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
```

```csharp
// Features/DocumentValidators.cs
using FluentValidation;
using Microsoft.Extensions.Options;
using PDS.Documents.Domain;
using PDS.Documents.Storage;

namespace PDS.Documents.Features;

internal abstract class DocumentMetadataValidator<T> : AbstractValidator<T> where T : IDocumentMetadataInput
{
    protected DocumentMetadataValidator(IOptions<DocumentsOptions> options)
    {
        var categories = options.Value.EffectiveCategories;
        RuleFor(x => x.Title).Cascade(CascadeMode.Stop)
            .Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("Title is required.")
            .Must(t => t.Trim().Length <= DocumentRules.TitleMaxLength)
            .WithMessage($"Use {DocumentRules.TitleMaxLength} characters or fewer.");
        RuleFor(x => x.Category).Cascade(CascadeMode.Stop)
            .Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Choose a category.")
            .Must(c => categories.Contains(c.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Choose one of: {string.Join(", ", categories)}.");
        RuleFor(x => x.Tags)
            .Must(t => DocumentRules.NormaliseTags(t).Count <= DocumentRules.MaxTags)
            .WithMessage($"Use {DocumentRules.MaxTags} tags or fewer.")
            .Must(t => DocumentRules.NormaliseTags(t).All(tag => tag.Length <= DocumentRules.TagMaxLength))
            .WithMessage($"Each tag must be {DocumentRules.TagMaxLength} characters or fewer.");
        RuleFor(x => x.Notes).MaximumLength(DocumentRules.NotesMaxLength);
        When(x => x.Link is not null, () =>
        {
            RuleFor(x => x.Link!.Module).NotNull().WithMessage("Choose the linked record's module.").IsInEnum();
            RuleFor(x => x.Link!.RecordId).NotEmpty().WithMessage("Choose a linked record.");
            RuleFor(x => x.Link!.Label).Cascade(CascadeMode.Stop)
                .Must(l => !string.IsNullOrWhiteSpace(l)).WithMessage("The linked record needs a label.")
                .Must(l => l.Trim().Length <= DocumentRules.LinkLabelMaxLength)
                .WithMessage($"Use {DocumentRules.LinkLabelMaxLength} characters or fewer.");
        });
    }
}

internal sealed class CreateDocumentValidator : DocumentMetadataValidator<CreateDocumentRequest>
{
    public CreateDocumentValidator(IOptions<DocumentsOptions> options) : base(options)
    {
        RuleFor(x => x.UploadId).NotEmpty().WithMessage("Upload the file first.");
        RuleFor(x => x.FileName).Must(n => FileTypes.ContentTypeFor(n) is not null)
            .WithMessage(_ => $"This file type isn't allowed. Allowed: {FileTypes.AllowedList}.");
    }
}

internal sealed class UpdateDocumentValidator : DocumentMetadataValidator<UpdateDocumentRequest>
{
    public UpdateDocumentValidator(IOptions<DocumentsOptions> options) : base(options) =>
        RuleFor(x => x.Version).GreaterThan(0);
}

internal sealed class AddVersionValidator : AbstractValidator<AddVersionRequest>
{
    public AddVersionValidator()
    {
        RuleFor(x => x.UploadId).NotEmpty().WithMessage("Upload the file first.");
        RuleFor(x => x.FileName).Must(n => FileTypes.ContentTypeFor(n) is not null)
            .WithMessage(_ => $"This file type isn't allowed. Allowed: {FileTypes.AllowedList}.");
        RuleFor(x => x.Version).GreaterThan(0);
    }
}

internal sealed class DeleteDocumentValidator : AbstractValidator<DeleteDocumentRequest>
{
    public DeleteDocumentValidator() => RuleFor(x => x.Version).GreaterThan(0);
}
```

Null `Title`/`Category` in the JSON bind as null. `Must(...IsNullOrWhiteSpace)` handles that, and `.Trim()` only runs after the cascade stops on null. The `Tags`/`Notes` rules must tolerate null.

- [ ] **Step 5: Upload confirmation.**

```csharp
// Features/UploadConfirmation.cs
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
```

- [ ] **Step 6: Endpoints.** Each one follows the Consents slice style (`Map` + `internal static Handle`). Write all seven:

```csharp
// Features/ListDocuments.cs
internal static class ListDocuments
{
    public static void Map(RouteGroupBuilder group) => group.MapGet("", Handle).WithName("ListDocuments");

    internal static async Task<Results<Ok<List<DocumentDetails>>, NotFound>> Handle(
        Guid projectId, DocumentStore store, IPortfolioQueries portfolio, CancellationToken ct)
    {
        if (await ProjectLookup.FindAsync(portfolio, projectId, ct) is null)
            return TypedResults.NotFound();
        var records = await store.ListForProjectAsync(new ProjectId(projectId), ct);
        return TypedResults.Ok(records
            .Where(r => !r.Document.IsDeleted)
            .OrderByDescending(r => r.Document.CurrentVersion.Uploaded.At)
            .Select(DocumentDetails.From)
            .ToList());
    }
}
```

```csharp
// Features/GetDocument.cs
internal static class GetDocument
{
    public static void Map(RouteGroupBuilder group) => group.MapGet("/{documentId:guid}", Handle).WithName("GetDocument");

    internal static async Task<Results<Ok<DocumentDetails>, NotFound>> Handle(
        Guid projectId, Guid documentId, DocumentStore store, CancellationToken ct) =>
        await store.GetAsync(new ProjectId(projectId), new DocumentId(documentId), ct) is { Document.IsDeleted: false } record
            ? TypedResults.Ok(DocumentDetails.From(record))
            : TypedResults.NotFound();
}
```

```csharp
// Features/CreateDocument.cs
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
            storage, options.Value.MaxFileSizeBytes, pid, documentId, request.UploadId, request.FileName, 1, stamp, ct);
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
```

`AddVersion` (`POST /{documentId:guid}/versions`, CanWrite, `WithValidation<AddVersionRequest>`, name `AddDocumentVersion`). Return type: `Results<Ok<DocumentDetails>, NotFound, ValidationProblem, ProblemHttpResult>`. Order:
1. Load the project: unknown → 404, archived → `ProjectArchived`.
2. `store.GetAsync`: null or deleted → 404.
3. `record.Document.Version != request.Version` → `VersionConflict()`. This is **before any copy**.
4. `ConfirmAsync(..., number: doc.CurrentVersion.Number + 1, ...)`, returning the problem if there is one.
5. `doc.AddVersion(upload.Version, stamp)`.
6. `store.AddVersionAsync(doc, request.Version)`. False → `VersionConflict()`; the pending object stays and the lifecycle expires it.
7. `DiscardPendingAsync`.
8. Return `Ok(DocumentDetails.From(new DocumentRecord(doc, [upload.Version, .. record.Versions])))`.

`UpdateDocument` (`PUT /{documentId:guid}`, CanWrite, `WithValidation<UpdateDocumentRequest>`, name `UpdateDocument`):
1. Project unknown → 404; archived → 400.
2. Document missing or deleted → 404.
3. Stale version → 409.
4. `doc.Update(request.ToFields(categories), stamp)`, then `store.UpdateAsync(doc, request.Version)`. False → 409.
5. Return `Ok(DocumentDetails.From(record))`. The record holds the same `Document` instance, now mutated.

`DeleteDocument` (`POST /{documentId:guid}/delete`, **CanAdminister**, `WithValidation<DeleteDocumentRequest>`, name `DeleteDocument`). Return type: `Results<NoContent, NotFound, ProblemHttpResult>`.
1. Document missing or deleted → 404.
2. Stale version → 409.
3. `MarkDeleted(stamp)`, then `UpdateAsync`. False → 409.
4. Return `NoContent`.

There's no project-archived check, so an Admin can still delete.

`DownloadVersion` (`GET /{documentId:guid}/versions/{number:int}/download`, CanRead, name `DownloadDocumentVersion`). Return type: `Results<Ok<DownloadLink>, NotFound>`.
1. The document is missing or deleted, or no version with that number exists in `record.Versions` (fall back to `CurrentVersion` when its number matches) → 404.
2. `expiresAt = now + 5 min`.
3. `url = await storage.CreateDownloadUrlAsync(v.StorageKey, v.FileName, v.ContentType, expiresAt, ct)`.
4. Return `Ok(new DownloadLink(url, expiresAt))`.

Add the needed `using`s to every file, following the Consents slices.

- [ ] **Step 7: Module registration.** In `AddDocumentsModule`, add `services.AddSingleton<DocumentStore>();`. In `MapDocumentsEndpoints`, after `RequestUpload.Map(group)`, add `ListDocuments.Map(group); GetDocument.Map(group); CreateDocument.Map(group); AddVersion.Map(group); UpdateDocument.Map(group); DeleteDocument.Map(group); DownloadVersion.Map(group);`.

  `MapPost("/uploads")` and `MapGet("/{documentId:guid}")` don't clash: the `guid` constraint rejects "uploads".

- [ ] **Step 8: Verify.** Run `dotnet format`, then `dotnet build` (0 warnings). With the API on 5086 (Development, Local storage), check the full round-trip:

```bash
B=localhost:5086/api/documents/projects/$PID/documents
T=$(curl -s -XPOST $B/uploads -H 'content-type: application/json' -d '{"fileName":"plan.pdf","sizeBytes":12}')
URL=$(echo $T | python3 -c 'import json,sys;print(json.load(sys.stdin)["uploadUrl"])'); UP=$(echo $T | python3 -c 'import json,sys;print(json.load(sys.stdin)["uploadId"])')
curl -s -XPUT "$URL" -H 'content-type: application/pdf' --data-binary 'hello world!'
curl -s -XPOST $B -H 'content-type: application/json' -d "{\"uploadId\":\"$UP\",\"fileName\":\"plan.pdf\",\"title\":\"Site plan\",\"category\":\"drawing\",\"tags\":[\"Stage 1\",\"stage 1\"],\"link\":{\"module\":\"Consent\",\"recordId\":\"$(uuidgen)\",\"label\":\"BC 1\"}}"
#  → 201, category "Drawing", tags ["Stage 1"], currentVersion 1, sizeBytes 12
#  same POST again → 400 uploadId (pending gone)
curl -s $B                                     # list shows it, with versions[1]
curl -s $B/<docId>/versions/1/download         # url; curl -sD- "<url>" → 200, Content-Disposition attachment, body "hello world!"
# new version: request a ticket, PUT, POST $B/<docId>/versions {"uploadId","fileName":"plan-rev-b.pdf","version":1} → currentVersion 2, versions [2,1]
#  same again with version 1 → 409
# PUT $B/<docId> with stale version → 409; with the current version → 200
# POST $B/<docId>/delete {"version":N} → 204; GET $B/<docId> → 404; list no longer has it
# fileName mismatch: ticket for a.pdf, PUT it, confirm with fileName "a.png" → 400 fileName (TypeMismatch) and the pending file is removed
```

  Also check an oversize upload. Restart the API with `Documents__MaxFileSizeBytes=10`, get a ticket for `sizeBytes` 5, `PUT` 12 bytes, then confirm: expect a 400 on `sizeBytes`.

- [ ] **Step 9: Commit:** `feat(documents): document register with confirm-upload, versions, soft delete and downloads`.

---

### Task 3: Infra — private documents bucket, lifecycle, CORS, least-privilege grants

**Files:**
- Create: `infra/PDS.Infra/DocumentsBucket.cs`
- Modify: `infra/PDS.Infra/AppStack.cs` (small additive block)

**Interfaces:**
- Consumes: the `Documents:BucketName` configuration key (env `Documents__BucketName`) and the key prefixes `pending/` and `files/` (Task 1 `StorageKeys`).
- Produces: a CloudFormation bucket with a CDK-generated name; the Lambda env `Documents__BucketName`; the `DocumentsBucket` output.

- [ ] **Step 1: Construct.**

```csharp
using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.S3;
using Constructs;

namespace PDS.Infra;

/// <summary>
/// Private bucket for document files. Browsers PUT to pending/ and GET from files/ through presigned URLs only;
/// unconfirmed uploads in pending/ expire after a day. The bucket name is generated (S3 names are global).
/// </summary>
public sealed class DocumentsBucket : Construct
{
    public DocumentsBucket(Construct scope, string id, DeploymentSettings settings, string siteUrl) : base(scope, id)
    {
        Bucket = new Bucket(this, "bucket", new BucketProps
        {
            BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
            Encryption = BucketEncryption.S3_MANAGED,
            EnforceSSL = true,
            ObjectOwnership = ObjectOwnership.BUCKET_OWNER_ENFORCED,
            RemovalPolicy = settings.IsProduction ? RemovalPolicy.RETAIN : RemovalPolicy.DESTROY,
            AutoDeleteObjects = !settings.IsProduction,
            Cors =
            [
                new CorsRule
                {
                    AllowedOrigins = [siteUrl],
                    AllowedMethods = [HttpMethods.PUT, HttpMethods.GET],
                    AllowedHeaders = ["content-type"],
                    ExposedHeaders = ["etag"],
                    MaxAge = 3600,
                },
            ],
            LifecycleRules =
            [
                new LifecycleRule
                {
                    Id = "expire-pending-uploads",
                    Prefix = "pending/",
                    Expiration = Duration.Days(1),
                    AbortIncompleteMultipartUploadAfter = Duration.Days(1),
                },
            ],
        });
    }

    public Bucket Bucket { get; }

    /// <summary>
    /// Exactly what the API needs: sign PUTs, head, copy-source and delete under pending/; copy-destination and
    /// sign GETs under files/. No ListBucket, no delete under files/ (documents are soft-deleted).
    /// </summary>
    public void GrantApi(IFunction api)
    {
        api.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Actions = ["s3:PutObject", "s3:GetObject", "s3:DeleteObject"],
            Resources = [Bucket.ArnForObjects("pending/*")],
        }));
        api.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Actions = ["s3:PutObject", "s3:GetObject"],
            Resources = [Bucket.ArnForObjects("files/*")],
        }));
    }
}
```

- [ ] **Step 2: AppStack wiring.** Add these additive lines:
  1. After `var siteUrl = ...;`: `var documents = new DocumentsBucket(this, "documents", settings, siteUrl);`
  2. After the `foreach` that sets `Tables__*` env vars: `environment["Documents__BucketName"] = documents.Bucket.BucketName;`
  3. After `table.GrantReadWriteData(api);` loop: `documents.GrantApi(api);`
  4. Next to the other outputs: `_ = new CfnOutput(this, "DocumentsBucket", new CfnOutputProps { Value = documents.Bucket.BucketName });`

  Don't change the HTTP API CORS: delete is `POST`.

- [ ] **Step 3: Verify.**
  - Run `dotnet build PDS.slnx` (0 warnings).
  - Run `cd web && npm run build`, then `dotnet publish src/PDS.Api -c Release -r linux-arm64 --self-contained false -o artifacts/api`, then `cd infra && npx --yes aws-cdk@2.1143.0 synth -c deployment=example-dev --quiet`.
  - Inspect `infra/cdk.out/example-dev-app.template.json`:
    - the bucket has `PublicAccessBlockConfiguration` all true, `BucketEncryption` AES256, a CORS rule with the CloudFront origin, and a lifecycle rule with `Prefix: pending/` and `ExpirationInDays: 1`
    - the bucket policy denies non-TLS
    - the Lambda env has `Documents__BucketName`
    - the role policy has exactly the two S3 statements above
    - there's a `documents` DynamoDB table in `example-dev-data`

  You can use `python3 -c` or `grep` to check.
- [ ] **Step 4: Commit:** `feat(documents): private documents bucket with pending lifecycle, CORS and scoped grants`.

---

### Task 4: Web — Documents section: list, search/filter, upload, download

Run `cd web && npm run gen:api` first (the Task 1–2 operations must be in `schema.d.ts`).

**Files:**
- Create: `web/src/features/documents/types.ts`, `api.ts`, `format.ts`, `documentFormModel.ts`, `DocumentMetadataFields.tsx`, `UploadDocumentModal.tsx`, `ProjectDocumentsSection.tsx`
- Modify: `web/src/features/portfolio/ProjectDetailPage.tsx`. Add one import and `<ProjectDocumentsSection projectId={p.id} projectArchived={p.isArchived} />` after Sales.
- Commit: the regenerated `web/src/shared/api/openapi.json` and `schema.d.ts`.

**Interfaces:**
- Consumes: the HTTP API from Tasks 1–2.
- Produces (for Task 5):
  - `DocumentDetails`, `DocumentSettings`, `DocumentLink`, `LinkedModule` types
  - hooks `useDocumentSettings()`, `useDocuments(projectId)`, `useUploadDocument(projectId)`, `useAddVersion(projectId)`, `useUpdateDocument(projectId)`, `useDeleteDocument(projectId)`, `useDownload(projectId)`
  - `DocumentFormValues`, `documentSchema`, `emptyDocumentValues`, `fromDocument`, `toMetadataInput`
  - `<DocumentMetadataFields form categories />`, with a `linkSlot` prop Task 5 fills
  - `formatBytes`

- [ ] **Step 1: types.ts**

```ts
import type { paths } from '../../shared/api/schema';
import type { DeepRequired } from '../../shared/api/types';

type ListPath = paths['/api/documents/projects/{projectId}/documents'];
type DocPath = paths['/api/documents/projects/{projectId}/documents/{documentId}'];

export type DocumentDetails = DeepRequired<ListPath['get']['responses'][200]['content']['application/json']>[number];
export type DocumentSettings = DeepRequired<paths['/api/documents/settings']['get']['responses'][200]['content']['application/json']>;
export type CreateDocumentInput = ListPath['post']['requestBody']['content']['application/json'];
export type UpdateDocumentInput = DocPath['put']['requestBody']['content']['application/json'];
export type DocumentLink = NonNullable<CreateDocumentInput['link']>;
export type LinkedModule = 'Consent' | 'Contract' | 'Risk';
export const linkedModuleLabel: Record<LinkedModule, string> = { Consent: 'Consent', Contract: 'Contract', Risk: 'Risk' };
```

Adjust the exact type paths to what `schema.d.ts` generated (for example, a nullable `link` may be `DocumentLink | null`). The file must typecheck.

- [ ] **Step 2: api.ts.** Upload is three steps in one mutation. The S3 `PUT` uses plain `fetch` with **no** Authorization header.

```ts
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { unwrap, useApi } from '../../shared/api/client';
import type { CreateDocumentInput, DocumentDetails, DocumentSettings, UpdateDocumentInput } from './types';

const documentsKey = (projectId: string) => ['documents', projectId];

type UploadTicket = { uploadId: string; uploadUrl: string; contentType: string; expiresAt: string };
export type DocumentMetadataInput = Omit<CreateDocumentInput, 'uploadId' | 'fileName'>;

export class UploadFailedError extends Error {
  constructor() {
    super('The upload failed. Try again.');
    this.name = 'UploadFailedError';
  }
}

function useRefresh(projectId: string) {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: documentsKey(projectId) });
}

export function useDocumentSettings() {
  const api = useApi();
  return useQuery({
    queryKey: ['documents', 'settings'],
    queryFn: () => unwrap<DocumentSettings>(api.GET('/api/documents/settings')),
    staleTime: 10 * 60_000,
  });
}

export function useDocuments(projectId: string) {
  const api = useApi();
  return useQuery({
    queryKey: documentsKey(projectId),
    queryFn: () =>
      unwrap<DocumentDetails[]>(api.GET('/api/documents/projects/{projectId}/documents', { params: { path: { projectId } } })),
  });
}

/** Requests a presigned URL and PUTs the file straight to storage. Returns the upload id to confirm. */
function useSendFile(projectId: string) {
  const api = useApi();
  return async (file: File): Promise<string> => {
    const ticket = await unwrap<UploadTicket>(
      api.POST('/api/documents/projects/{projectId}/documents/uploads', {
        params: { path: { projectId } },
        body: { fileName: file.name, sizeBytes: file.size },
      }),
    );
    let response: Response;
    try {
      response = await fetch(ticket.uploadUrl, { method: 'PUT', headers: { 'Content-Type': ticket.contentType }, body: file });
    } catch {
      throw new UploadFailedError();
    }
    if (!response.ok) throw new UploadFailedError();
    return ticket.uploadId;
  };
}

export function useUploadDocument(projectId: string) {
  const api = useApi();
  const send = useSendFile(projectId);
  const refresh = useRefresh(projectId);
  return useMutation({
    mutationFn: async ({ file, metadata }: { file: File; metadata: DocumentMetadataInput }) => {
      const uploadId = await send(file);
      return unwrap<DocumentDetails>(
        api.POST('/api/documents/projects/{projectId}/documents', {
          params: { path: { projectId } },
          body: { ...metadata, uploadId, fileName: file.name },
        }),
      );
    },
    onSettled: refresh,
  });
}

export function useAddVersion(projectId: string) {
  const api = useApi();
  const send = useSendFile(projectId);
  const refresh = useRefresh(projectId);
  return useMutation({
    mutationFn: async ({ documentId, file, version }: { documentId: string; file: File; version: number }) => {
      const uploadId = await send(file);
      return unwrap<DocumentDetails>(
        api.POST('/api/documents/projects/{projectId}/documents/{documentId}/versions', {
          params: { path: { projectId, documentId } },
          body: { uploadId, fileName: file.name, version },
        }),
      );
    },
    onSettled: refresh,
  });
}

export function useUpdateDocument(projectId: string) {
  const api = useApi();
  const refresh = useRefresh(projectId);
  return useMutation({
    mutationFn: ({ documentId, body }: { documentId: string; body: UpdateDocumentInput }) =>
      unwrap<DocumentDetails>(
        api.PUT('/api/documents/projects/{projectId}/documents/{documentId}', { params: { path: { projectId, documentId } }, body }),
      ),
    onSettled: refresh,
  });
}

export function useDeleteDocument(projectId: string) {
  const api = useApi();
  const refresh = useRefresh(projectId);
  return useMutation({
    mutationFn: ({ documentId, version }: { documentId: string; version: number }) =>
      unwrap<void>(
        api.POST('/api/documents/projects/{projectId}/documents/{documentId}/delete', {
          params: { path: { projectId, documentId } },
          body: { version },
        }),
      ),
    onSettled: refresh,
  });
}

/** Fetches a short-lived download URL, then navigates to it (the response is an attachment, so the page stays). */
export function useDownload(projectId: string) {
  const api = useApi();
  return useMutation({
    mutationFn: async ({ documentId, number }: { documentId: string; number: number }) => {
      const link = await unwrap<{ url: string; expiresAt: string }>(
        api.GET('/api/documents/projects/{projectId}/documents/{documentId}/versions/{number}/download', {
          params: { path: { projectId, documentId, number } },
        }),
      );
      window.location.assign(link.url);
    },
  });
}
```

- [ ] **Step 3: format.ts:** `export function formatBytes(bytes: number, culture: string): string`. Under 1024 → "n B". Otherwise use KB, MB or GB with one decimal through `Intl.NumberFormat(culture, { maximumFractionDigits: 1 })`.

- [ ] **Step 4: documentFormModel.ts**

```ts
import { z } from 'zod';
import type { DocumentMetadataInput } from './api';
import type { DocumentDetails, LinkedModule } from './types';

export type LinkValue = { module: LinkedModule; recordId: string; label: string } | null;

export type DocumentFormValues = { title: string; category: string | null; link: LinkValue; tags: string[]; notes: string };

export const emptyDocumentValues: DocumentFormValues = { title: '', category: null, link: null, tags: [], notes: '' };

/** Mirrors the API's DocumentMetadataValidator (the category list comes from the server settings). */
export const documentSchema = z.object({
  title: z.string().trim().min(1, 'Title is required').max(200, 'Use 200 characters or fewer'),
  category: z.string({ error: 'Choose a category' }).min(1, 'Choose a category'),
  link: z.any(),
  tags: z
    .array(z.string().max(40, 'Each tag must be 40 characters or fewer'))
    .max(10, 'Use 10 tags or fewer'),
  notes: z.string().max(2000, 'Use 2000 characters or fewer'),
});

export function toMetadataInput(v: DocumentFormValues): DocumentMetadataInput {
  return {
    title: v.title.trim(),
    category: v.category ?? '',
    link: v.link,
    tags: v.tags,
    notes: v.notes.trim() === '' ? null : v.notes.trim(),
  };
}

export function fromDocument(d: DocumentDetails): DocumentFormValues {
  return {
    title: d.title,
    category: d.category,
    link: d.link ? { module: d.link.module as LinkedModule, recordId: d.link.recordId, label: d.link.label } : null,
    tags: d.tags,
    notes: d.notes ?? '',
  };
}

/** "Site plan rev B.pdf" → "Site plan rev B". */
export function titleFromFileName(name: string): string {
  return name.replace(/\.[^.]+$/, '');
}
```

- [ ] **Step 5: DocumentMetadataFields.tsx.** This renders the form fields shared by the upload and edit modals:
  - Title `TextInput` (required)
  - Category `Select` (required, data = `categories`)
  - an optional `linkSlot?: ReactNode`, rendered between Category and Tags (Task 5 fills it; Task 4 passes nothing)
  - Tags `TagsInput` (`maxTags={10}`, `splitChars={[',']}`, description "Press Enter or comma to add")
  - Notes `Textarea`

  Props: `{ form: UseFormReturnType<DocumentFormValues>; categories: string[]; linkSlot?: ReactNode }`. Use `form.getInputProps('title')` and so on.

  A stored category that isn't in the settings must still show. Pass `data={[...new Set([...categories, ...(form.values.category ? [form.values.category] : [])])]}`.

- [ ] **Step 6: UploadDocumentModal.tsx.** Use the keyed-inner pattern from `RiskFormModal`. Props: `{ projectId; opened: boolean; onClose; settings: DocumentSettings }`. The modal title is "Upload document".
  - `FileInput` labelled "File", required. Its description is `Up to {formatBytes(max)}. Allowed: {extensions.join(', ')}` and `accept` is the extensions prefixed with `.`.
  - Choosing a file sets `title` to `titleFromFileName` when the title is empty.
  - Before submitting, check on the client:
    - no file → error "Choose a file"
    - an extension not in `settings.allowedExtensions` (case-insensitive) → "This file type isn't allowed"
    - `file.size > settings.maxFileSizeBytes` → "The file is larger than the N MB limit"
    - `file.size === 0` → "The file is empty"

    These go in local `fileError` state, shown on the `FileInput`.
  - Submit calls `upload.mutateAsync({ file, metadata: toMetadataInput(values) })`. While pending, the button shows "Uploading…" (`loading`).
  - Success: a green notification "Document uploaded", then close.
  - Errors:
    - `UploadFailedError` → a top `Alert` with its message
    - an `ApiError` 400 whose field is `fileName`, `sizeBytes` or `uploadId` → show it on the `FileInput` (`fileError`)
    - anything else → `handleMutationError(error, { form, setTopError, onClose, failureTitle: 'Could not upload the document', fieldNames: Object.keys(form.values), entity: 'document' })`

    For the 400 split: read `serverFieldErrors(error.problem)`. Put the file keys into `fileError` and pass the rest through `handleMutationError`. Doing it in that order means the file keys don't also show as a top error. When file keys exist, it's fine to call `form.setErrors` with the remaining keys only.

- [ ] **Step 7: ProjectDocumentsSection.tsx.** Props: `{ projectId: string; projectArchived: boolean }`.
  - **Card header:** `Title order={4}` "Documents", then a `Group` with:
    - a `TextInput` (placeholder "Search documents", `w={220}`)
    - a `Select` (placeholder "All categories", clearable, data = the distinct categories present in the list)
    - `Button` "Upload document", shown when `canWrite && !projectArchived && settings.data`
  - **Filtering** happens in the client. The case-insensitive search term must appear in the title, file name, any tag or the link label, and the category must equal the filter when one is set.
  - **States:**
    - error → red `Alert` "Could not load documents"
    - loading → `Loader`
    - empty list → `Text c="dimmed"` "No documents uploaded."
    - no matches → "No documents match."
  - **Table** (inside `Table.ScrollContainer minWidth={760}`):
    - Title: `Text fw={500}`, with tags as `Badge size="xs" variant="light"` in a `Group gap={4}` below and notes `lineClamp={1}` dimmed
    - Category
    - Linked to: `link ? `${link.module} · ${link.label}` : '—'`
    - Version: `v{currentVersion}`
    - Size: `formatBytes`
    - Uploaded: `formatDateTime(uploadedAt)` then `Text size="xs" c="dimmed"` uploadedBy
    - Actions: `Button size="xs" variant="light"` "Download", which calls `download.mutate({ documentId, number: currentVersion })`. It's loading while that row's download is pending, and on error shows a red notification "Could not download the file".
  - Render `<UploadDocumentModal ...>` when open.

  Use `useConfig()` for culture and time zone.

- [ ] **Step 8: ProjectDetailPage.** One import line and one render line, after `ProjectSalesSection`.

- [ ] **Step 9: Verify.**
  - `npm run gen:api && npm run typecheck && npm run lint && npm run build` all pass.
  - Optional but recommended: run Vite on 5196 with the proxy pointed at 5086 through a CLI/env override, without editing `vite.config.ts`. For example, write a temp config in the scratchpad that imports the real one and overrides `server.proxy`. Then upload a PDF, see it listed and download it.

- [ ] **Step 10: Commit:** `feat(documents): project documents section with upload and download`.

---

### Task 5: Web — linked record picker, edit, new version, versions list, delete

**Files:**
- Create: `web/src/features/documents/LinkedRecordSelect.tsx`, `EditDocumentModal.tsx`, `NewVersionModal.tsx`, `VersionsModal.tsx`, `DeleteDocumentModal.tsx`
- Modify: `UploadDocumentModal.tsx` (pass `linkSlot`), `ProjectDocumentsSection.tsx` (more actions + modals)

**Interfaces:**
- Consumes: Task 4's hooks, form model and `DocumentMetadataFields`. It also reads three existing endpoints, through `useApi()` directly; **don't import other features' modules**:
  - `/api/consents/projects/{projectId}/consents`
  - `/api/contractors/projects/{projectId}/contracts`
  - `/api/risks/projects/{projectId}/risks?includeClosed=true`
- Produces: the finished UI.

- [ ] **Step 1: LinkedRecordSelect.tsx.** Props: `{ projectId: string; value: LinkValue; onChange: (v: LinkValue) => void; error?: ReactNode }`.
  - **Data:** three `useQuery` calls with keys `['documents', 'link-options', projectId, 'consents'|'contracts'|'risks']`, each mapping its results to `{ value: `${module}:${id}`, label }`:
    - consents: `${c.title}${c.reference ? ` (${c.reference})` : ''}`
    - contracts: `${c.title} · ${c.contractorName}`
    - risks: `r.title`
  - Group the options as `[{ group: 'Consents', items }, { group: 'Contracts', items }, { group: 'Risks', items }]`, leaving out empty groups.
  - **If the current `value` isn't among the loaded options** (the record was renamed or removed), add an option `{ value: `${module}:${recordId}`, label: `${value.label} (saved)` }` to the matching group, so the selection still shows.
  - **Control:** `Select` labelled "Linked record", `searchable`, `clearable`, placeholder "None", description "Optional: a consent, contract or risk on this project".
  - **onChange:** split on the first `:`. Find the option's label and strip a trailing " (saved)". Call `onChange({ module, recordId, label })`, or `onChange(null)` when cleared.
  - **Loading and errors:** `disabled` while any query is loading. A failed query just contributes no items; the field must not block saving.

- [ ] **Step 2: Wire the picker into Upload and Edit.** Pass this as `DocumentMetadataFields`'s `linkSlot`:

```tsx
<LinkedRecordSelect projectId={projectId} value={form.values.link} onChange={(v) => form.setFieldValue('link', v)} error={form.errors['link'] ?? form.errors['link.label'] ?? form.errors['link.recordId']} />
```

  Add `'link.module'`, `'link.recordId'` and `'link.label'` to `fieldNames` in both modals, so server link errors show on the picker rather than as a top alert. Their message goes to the `link` error.

- [ ] **Step 3: EditDocumentModal.tsx.** Keyed inner component (`key = `${doc.id}-${doc.version}``). Props: `{ projectId; document: DocumentDetails | undefined; settings: DocumentSettings; onClose }`.
  - Title "Edit document".
  - Initial values come from `fromDocument`, validated with `zodValidate(documentSchema)`. The fields are `DocumentMetadataFields` with the picker.
  - Submit calls `update.mutateAsync({ documentId, body: { ...toMetadataInput(values), version: document.version } })`.
  - Success: green "Document saved".
  - Errors: `handleMutationError` with `entity: 'document'` and `failureTitle: 'Could not save the document'`.

- [ ] **Step 4: NewVersionModal.tsx.** Props: `{ projectId; document; settings; onClose }`.
  - Title `New version of “{title}”`.
  - The text "Currently v{n}: {fileName}".
  - The same `FileInput` and client checks as the upload modal. Extract a small `validateFile(file, settings): string | null` helper into `format.ts`, and use it from both modals.
  - Submit calls `addVersion.mutateAsync({ documentId, file, version: document.version })`.
  - Success: green "Version {n+1} uploaded".
  - Errors:
    - `UploadFailedError` → top alert
    - 400 on `fileName`, `sizeBytes`, `uploadId` or `projectId` → file error
    - 409 → yellow toast "This document was changed by someone else", then close
    - anything else → red toast

- [ ] **Step 5: VersionsModal.tsx.** Props: `{ projectId; document | undefined; onClose }`.
  - A table with columns Version (`v{number}`, plus a "Current" `Badge` on the current one), File, Size, Uploaded (date-time · by), and a Download button (it uses `useDownload`).
  - Title `Versions of “{title}”`.

- [ ] **Step 6: DeleteDocumentModal.tsx.** Props: `{ projectId; document | undefined; onClose }`.
  - The text: `Delete “{title}”? It will no longer appear in the register. Files are kept and an administrator can restore them on request.`
  - Buttons: a red `Button` "Delete" and "Cancel".
  - Success: green "Document deleted", then close.
  - 409: yellow toast, then close. Anything else: red toast "Could not delete the document".

- [ ] **Step 7: Section actions.** In each row, after Download, add:
  - "Versions", shown when `versions.length > 1` (variant default)
  - for `canWrite && !projectArchived`: "Edit" (light) and "New version" (default)
  - for `canAdminister`: "Delete" (`color="red" variant="subtle"`)

  Keep the actions in a `Group gap="xs" wrap="nowrap" justify="flex-end"`. Hold `editing`, `newVersionOf`, `versionsOf` and `deleting` in state, and render the four modals.

- [ ] **Step 8: Verify.**
  - `npm run gen:api && npm run typecheck && npm run lint && npm run build` all pass.
  - Manual check, if Vite is running:
    - link a document to a consent
    - edit its tags
    - upload v2, open Versions, download v1
    - delete it as Admin
  - Then do the final full verification: `dotnet format PDS.slnx --verify-no-changes`, `dotnet build PDS.slnx`, and the synth from Task 3.

- [ ] **Step 9: Commit:** `feat(documents): linked records, edit, versions and admin delete in the documents section`.

---

## Spec coverage

| Spec item | Task |
|---|---|
| Module, table, Contracts-only dependency | 1, 2 |
| Presigned PUT and GET, bytes never through the Lambda | 1 (upload), 2 (download) |
| Bucket security, CORS, lifecycle, least privilege | 3 |
| Categories configurable, settings endpoint | 1 |
| Title, category, link, tags, notes; uploaded-by/at, size, type | 2 (API), 4–5 (UI) |
| Versioning | 2 (API), 5 (UI) |
| Typed link `{module, recordId, label}` with no existence check | 2 (API), 5 (picker) |
| Size limit and type allowlist | 1 (request), 2 (confirm), 4 (client checks) |
| Abandoned uploads (pending/ + confirm + lifecycle) | 1, 2, 3 |
| Roles; soft delete by Admin | 2 (API), 5 (UI) |
| Archived-project rules | 1, 2 |
| Local dev storage | 1 |
| Documents section on project detail | 4, 5 |
