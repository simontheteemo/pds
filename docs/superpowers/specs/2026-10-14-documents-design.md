# PDS Documents (v1) — Design Spec

- Date: 2026-10-14
- Author: @simon (with Claude)
- Status: **Proceeding on the user's "autonomous, verify later" instruction.** Every decision below was made by Claude and is open to review.
- Builds on: [Foundation spec](2026-09-29-foundation-design.md) · [Risk Registry spec](2026-09-30-risk-registry-design.md) · [Consents spec](2026-10-14-consents-design.md)

## 1. Purpose

Project files (consent decisions, contracts, drawings, reports, invoices, photos) live today in email threads and shared drives. This module gives every project a **document register**: one place to upload a file, describe it, find it again and download it, with a simple version history when a revised file replaces an earlier one.

The architecture set says documents "go to S3 later and will dominate storage". So file bytes go straight from the browser to a private S3 bucket and back through presigned URLs. **File bytes never pass through the Lambda.** Only metadata goes through the API, into the module's own DynamoDB table.

### Stated vs decided

| Stated (task brief / architecture) | Decided by Claude (review later) |
|---|---|
| Documents in S3, presigned URLs, bytes never through the Lambda | The browser `PUT`s to a presigned S3 URL and downloads through a presigned `GET`. The API only signs URLs and does server-side `HeadObject` / `CopyObject` / `DeleteObject`, which move no bytes through the function |
| Metadata in the module's own table; files in a new private bucket | One module, `PDS.Documents`, owns the `documents` table (no GSI). One new bucket (CDK-generated name, since S3 names are global) is created in the app stack by a small `DocumentsBucket` construct |
| Bucket: block public access, encryption, CORS limited to the SPA origin, least-privilege API access | `BLOCK_ALL`, SSE-S3 (`S3_MANAGED`, so no KMS permissions are needed), `EnforceSSL`. CORS allows only the CloudFront site URL, for `PUT` and `GET`, with the `content-type` header. The API role gets `s3:PutObject`/`GetObject`/`DeleteObject` on `pending/*` and `s3:PutObject`/`GetObject` on `files/*`. There's no `ListBucket` and no delete on `files/*`. In prod: `RETAIN`, no auto-delete. In dev: `DESTROY` + auto-delete, like the site bucket |
| Title, category (generic and configurable), optional link to another module's record, tags or notes | `Title` (required, ≤ 200). `Category` is a string that must be one of a **configured list**, `Documents:Categories`, defaulting to `Contract, Consent, Drawing, Report, Invoice, Photo, Correspondence, Other`. The list is served to the UI by `GET /api/documents/settings`, so a deployment can change it with configuration and no code change. Both `Tags` (≤ 10, each ≤ 40, de-duplicated case-insensitively) and `Notes` (≤ 2000) are included. Tags are cheap and make the list's search useful |
| Uploaded by/at, size, content type | Recorded per **version**: file name, size, content type, uploaded at/by. The document shows its current version's values |
| Simple versioning, or defer it | **Included.** "Upload new version" adds version n+1 and makes it current. Earlier versions stay downloadable from a version list. Drawings and reports are revised constantly, and "which file is current" is the main reason a register beats a shared drive. The cost is one extra item type and one endpoint. A version can't be deleted or reverted in v1; uploading the old file again creates a new version |
| Link to Consents/Contracts/Risks records: typed reference only, or check existence? | **Typed reference only:** `{ module, recordId, label }`, where `module` is one of `Consent`, `Contract`, `Risk`. The API **doesn't check that the record exists**: it depends only on `PDS.Portfolio.Contracts` and no other module. The UI picker lists the project's consents, contracts and risks through the existing read APIs and stores a **label snapshot** (for example "Building consent · Stage 1 BC"), so the register shows the link without a cross-module join. Why: those modules expose no `Contracts/` query interfaces today, and adding three just for a soft link would mean editing three sibling modules. **Cost if wrong:** a link can go stale (renamed or removed record). It's informational only, and v2 can add `Exists` queries when they exist |
| Upload size limit and allowed-content-type policy | **Max 100 MiB per file** (`Documents:MaxFileSizeBytes`), enforced twice: when the URL is requested (declared size) and again on confirm (`HeadObject` actual size; an over-size object is deleted). The allowed types are an **extension allowlist** that maps to a server-chosen canonical content type (§3.4). The browser's guess is ignored. The canonical type is **signed into the upload URL**, so S3 rejects a `PUT` with any other `Content-Type`. HTML, SVG, scripts and executables are not allowed |
| Abandoned uploads: lifecycle on a `pending/` prefix, or a confirm step? | **Both.** Uploads land in `pending/<projectId>/<uploadId>`. Only the confirm call (create document / add version) copies the object to `files/…`, deletes the pending object and writes metadata. A lifecycle rule **expires `pending/` after 1 day** and aborts incomplete multipart uploads after 1 day. An abandoned upload leaves no metadata and is gone within about a day |
| Viewer reads/downloads; Manager/Admin upload and edit; delete Admin-only, soft or hard | **Soft delete.** `POST …/{id}/delete` (CanAdminister) marks the document deleted (`deleted` audit stamp). It disappears from every list and download, and its S3 objects are kept. Why: every other module avoids hard deletes (projects archive, risks close). An accidental delete of a signed contract must be recoverable. And the API role then needs no delete permission on `files/*`. There's no restore endpoint in v1; an operator can clear the flag in DynamoDB. **Cost if wrong:** deleted files keep costing storage (cents per GB per month) |
| Local dev: DynamoDB Local only; no S3 emulator | **`Documents:Storage = Local`**, behind a small `IFileStorage` interface. It's set in `appsettings.Development.json`, and startup refuses it outside Development (the same rule as `Auth:Mode=Development`). Local "presigned" URLs point at anonymous API endpoints `/api/documents/local-files/{key}` guarded by an HMAC signature and an expiry. Files go to a temp directory. Locally the bytes *do* pass through the API, which is fine for dev. The browser flow is identical. No MinIO container, no new infrastructure |
| Web: a Documents section on project detail; optionally a cross-project page | Documents section only (below Sales). The cross-project page is **deferred**: it needs a Scan or GSI design, and a search across projects isn't asked for yet |
| Archived projects | Readable and downloadable. No uploads, new versions or edits (400 on `projectId`), the same rule as other modules. Admin delete still works |
| No new tests (standing directive) | Verify by format, build, gen:api, typecheck, lint, web build, synth and manual API checks (including a full local upload → confirm → download round-trip) |

### Success criteria

- On a project's detail page, a Manager can:
  - upload a file with a title, category, optional linked record, tags and notes
  - see it listed with category, current version, size, uploader and date, and download it
  - edit its details, upload a new version, and download any earlier version
- A Viewer can list and download, with no write actions. An Admin can also delete a document; it then disappears.
- The browser's upload goes straight to S3 in AWS (presigned `PUT`). The Lambda's request and response bodies never contain file bytes.
- An upload that's never confirmed leaves no metadata, and its object expires within about a day.
- CDK creates the `documents` table through the existing table loop, and the bucket with the stated security settings. The only shared-file edits are one line each in the host, solution, infra program, routes and project detail page, plus a small additive block in `AppStack`.

## 2. Architecture

```
Browser ──(1) POST /uploads {fileName,size} ──► API Lambda ── signs PUT URL for pending/<pid>/<uploadId>
   │◄──────────── { uploadId, url, contentType, expiresAt }
   ├──(2) PUT file bytes (Content-Type signed) ───────────────────────────────► S3 pending/…
   ├──(3) POST /documents {uploadId, title, …} ──► API Lambda ── HeadObject (size/type check)
   │                                                           ── CopyObject pending → files/<pid>/<docId>/<uploadId>
   │                                                           ── DeleteObject pending
   │                                                           ── TransactWrite Document + Version items ──► DynamoDB
   └──(4) GET …/versions/{n}/download ──► API Lambda ── signs GET URL (attachment; filename) ──► browser navigates to S3
```

- A new module, `src/Modules/PDS.Documents`, with the same shape as Consents:
  - `Contracts/` (public) holds `DocumentId`.
  - `Domain/`, `Data/`, `Features/` and `Storage/` are internal.
  - `DocumentsModule.cs` is public: `Tables`, `AddDocumentsModule(IConfiguration)` and `MapDocumentsEndpoints()`.
- It references `PDS.Portfolio` for Contracts only. Its one new package is **`AWSSDK.S3`** (4.0.103.4, the official SDK). That version uses the same `AWSSDK.Core` as the existing DynamoDB package. There's no lighter way to sign S3 URLs or call S3.
- `Storage/IFileStorage`: `CreateUploadUrl`, `CreateDownloadUrl`, `Head`, `Copy` and `Delete`, with two implementations:
  - `S3FileStorage` (bucket from `Documents:BucketName`)
  - `LocalFileStorage` plus `LocalFileEndpoints` (Development only)
- Infra:
  - `infra/PDS.Infra/DocumentsBucket.cs` (new) builds the bucket, lifecycle rule, CORS and grants.
  - `AppStack` adds a few lines that create it and set `Documents__BucketName` on the function.
  - `Program.cs` adds `.. DocumentsModule.Tables`.
- Web: `web/src/features/documents`, with `ProjectDocumentsSection` rendered on the project detail page.

## 3. Data

### 3.1 Domain

**Document**

| Field | Rules |
|---|---|
| `Id` | `DocumentId`, UUIDv7 |
| `ProjectId` | |
| `Title` | Required, trimmed, ≤ 200 |
| `Category` | Required; must be in the configured list at write time. A stored category later removed from config still displays |
| `Link` | `DocumentLink?`: `Module` (`Consent` / `Contract` / `Risk`), `RecordId` (Guid), `Label` (required, ≤ 200) |
| `Tags` | ≤ 10, each trimmed, 1–40 chars, de-duplicated case-insensitively (first spelling kept) |
| `Notes` | ≤ 2000 |
| `CurrentVersion` | `DocumentVersion` (the latest) |
| `Deleted` | `AuditStamp?` |
| Other | `Created`, `Updated`, `Version` (optimistic concurrency, separate from the file version number) |

**DocumentVersion** (immutable once written)

| Field | Rules |
|---|---|
| `Number` | 1, 2, 3, … |
| `FileName` | Original name, ≤ 255, path segments stripped |
| `ContentType` | Canonical, from the extension allowlist |
| `SizeBytes` | The size S3 reports on confirm |
| `StorageKey` | `files/<projectId>/<documentId>/<uploadId>` |
| `Uploaded` | `AuditStamp` |

### 3.2 Table `documents`

Key schema `pk`/`sk`, **no GSI**. An `itemType` attribute (`Document` / `Version`) distinguishes the two item types.

| Item | pk | sk |
|---|---|---|
| Document | `PROJECT#<projectId>` | `DOC#<documentId>` |
| Version | `PROJECT#<projectId>` | `DOC#<documentId>#V#<number:D6>` |

The document item stores the current version's fields inline (`fileName`, `contentType`, `sizeBytes`, `storageKey`, `versionNumber`, `uploadedAt`, `uploadedBy`), so the list never has to join.

| Access pattern | Operation |
|---|---|
| List a project's documents | `Query pk, begins_with(sk, "DOC#")` (all pages), keep only `Document` items that aren't deleted. Versions are read too but ignored; fine at the expected volume |
| One document with its versions | `Query pk, begins_with(sk, "DOC#<id>")`. The ids are fixed-length GUIDs, so the prefix can't match another document |
| Create | `TransactWriteItems`: Put document `attribute_not_exists(pk)` + Put version 1 `attribute_not_exists(pk)` |
| New version | `TransactWriteItems`: Put document `#version = :expected` + Put version n+1 `attribute_not_exists(pk)`. A cancelled transaction → 409 |
| Edit / delete | `PutItem` with `#version = :expected` → 409 on mismatch |

### 3.3 Storage keys and lifecycle

- `pending/<projectId>/<uploadId>`: the presigned `PUT` target. Lifecycle expires these after **1 day**; incomplete multipart uploads are aborted after 1 day.
- `files/<projectId>/<documentId>/<uploadId>`: the stored file, copied server-side on confirm. It's never overwritten (each version has its own key) and never deleted by the app.
- Upload URL lifetime: **15 minutes**. Download URL lifetime: **5 minutes**. Downloads are signed with `response-content-disposition: attachment; filename*=UTF-8''<name>` and the stored content type, so nothing renders inline from the bucket's origin.

### 3.4 Allowed file types

Lower-case extension → canonical content type:

| Group | Extensions |
|---|---|
| PDF | `pdf` → `application/pdf` |
| Images | `jpg`/`jpeg` → `image/jpeg`, `png`, `gif`, `webp`, `heic`, `tif`/`tiff` → `image/tiff` |
| Office | `doc`, `docx`, `xls`, `xlsx`, `ppt`, `pptx` (their standard MIME types), `csv` → `text/csv`, `txt` → `text/plain`, `rtf` → `application/rtf` |
| Mail | `msg` → `application/vnd.ms-outlook`, `eml` → `message/rfc822` |
| Drawings | `dwg` → `image/vnd.dwg`, `dxf` → `image/vnd.dxf` |
| Archive | `zip` → `application/zip` |

Anything else → 400 on `fileName`: "This file type isn't allowed. Allowed: …". The list lives in one place (`FileTypes`) and is served to the UI with the settings.

## 4. API

`GET /api/documents/settings` (CanRead) returns `{ categories[], maxFileSizeBytes, allowedExtensions[] }`.

The project-scoped group is `/api/documents/projects/{projectId}/documents`, and it requires CanRead.

| Method | Route | Policy | Behaviour |
|---|---|---|---|
| GET | `` | CanRead | 404 for an unknown project. Returns `DocumentListItem[]`, not deleted, ordered by most recent upload first |
| GET | `/{documentId}` | CanRead | `DocumentDetails` (list fields + `versions[]`, newest first) or 404 (deleted → 404) |
| POST | `/uploads` | CanWrite | Body `{ fileName, sizeBytes }`. 404 unknown project; 400 `projectId` if archived; 400 `fileName` for a disallowed type; 400 `sizeBytes` if ≤ 0 or > max. Returns `{ uploadId, uploadUrl, contentType, expiresAt }`. The client must `PUT` with exactly `Content-Type: <contentType>` |
| POST | `` | CanWrite | Body `{ uploadId, fileName, title, category, link?, tags, notes }`. Confirms the upload and creates the document with version 1. 400 `uploadId` "The upload wasn't found or has expired. Upload the file again." when the pending object is missing; 400 `sizeBytes` if it's too large (the object is deleted). `201 DocumentDetails` |
| POST | `/{documentId}/versions` | CanWrite | Body `{ uploadId, fileName, version }`. Same confirm checks; 404 unknown or deleted doc; 400 archived; 409 stale. `200 DocumentDetails` |
| PUT | `/{documentId}` | CanWrite | Body `{ title, category, link?, tags, notes, version }`. 404, 400 archived, 409 stale |
| POST | `/{documentId}/delete` | CanAdminister | Body `{ version }`. 409 stale. `204` |
| GET | `/{documentId}/versions/{number}/download` | CanRead | `{ url, expiresAt }` or 404. The client then navigates to the URL. A JSON body is used instead of a redirect because `fetch` with a bearer token can't hand a redirect to the browser as a download |

**Upload id binding.** The pending key includes the project id, so an `uploadId` can only be confirmed on the project it was issued for. The confirm call's `fileName` is re-validated, and its canonical content type must equal the stored object's type. Confirming the same `uploadId` twice fails: the second time the pending object is gone, so it gets the "not found" 400.

**Ordering on confirm:** Head → size/type check → Copy → write metadata → Delete pending (best-effort).
- If the metadata write fails, an orphan copy is left under `files/`. That's rare, costs only storage, and doesn't affect correctness. The pending object then expires.
- If the pending delete fails, the lifecycle cleans it up.

Errors follow the ProblemDetails conventions. A 409 says "This document was changed by someone else."

### 4.1 Local storage (Development only)

- `Documents:Storage` is `S3` (the default) or `Local`. `Local` + a non-Development environment → startup fails with options validation.
- `LocalFileStorage` writes to `Documents:LocalPath`, defaulting to `<temp>/pds-documents`. Each object's content type goes in a `.type` sidecar file.
- Signed URLs are built from the request's scheme and host, so the Vite proxy works: `…/api/documents/local-files/{key}?expires=<unix>&sig=<hmac>`.
  - The HMAC-SHA256 key is random per process.
  - The signature covers the method, key, expiry and content type.
- `PUT` and `GET` on `local-files/{**key}` are mapped **only in Local mode**. They're `AllowAnonymous`, like a real presigned URL.
- `PUT` rejects a mismatched `Content-Type` (mirroring S3's signature failure). `GET` returns the file as an attachment. Keys must resolve inside the root directory.

## 5. UI

### Project detail: Documents section (below Sales)

- **Card header:** "Documents", a search box (title, file name, tags, link label; matched in the client), a category filter, and "Upload document" (canWrite and project not archived).
- **Table:** Title (with tag badges beneath), Category, Linked to (label), Version (`v3`), Size, Uploaded (date · by), and Actions:
  - Download
  - Edit (canWrite)
  - New version (canWrite)
  - Versions
  - Delete (canAdminister, with a confirm modal)

  Empty state: "No documents uploaded."
- **Upload modal:** File input (it shows the allowed types and max size from the settings, and checks both on the client before requesting a URL), Title (defaults to the file name without its extension), Category, Linked record (optional), Tags (`TagsInput`), Notes. On submit it runs three steps: request a URL → `PUT` with `fetch` → confirm. A progress state shows "Uploading…". An S3 failure shows "The upload failed. Try again."
- **Linked record picker:** a searchable `Select` grouped by Consents / Contracts / Risks, loaded from the existing project-scoped list endpoints. Its value stores `{ module, recordId, label }`.
- **Edit modal:** the same metadata fields, without the file.
- **New version modal:** a file input only.
- **Versions modal:** a table of v#, file name, size, uploaded (date · by), and Download.
- **Errors:** a 400 maps onto fields, an unmapped key goes to a top alert, and a 409 shows a toast, closes the modal and refreshes. This is the risks/consents helper, copied into the feature as `documentMutationError.ts`, because the shared helper cleanup hasn't landed.

## 6. Out of scope (v1)

- A cross-project documents page or search
- Restoring deleted documents (an operator can do it in DynamoDB), deleting single versions, and reverting
- Existence checks or deep links for linked records; links to Contractors or Sales units
- Previews or thumbnails, and inline viewing
- Virus scanning (v2 option: GuardDuty Malware Protection for S3, which is managed and fits the pending → files step)
- Storage-class transitions (Intelligent-Tiering) and per-deployment category lists in the CDK deployment file. Categories can already be set with `Documents__Categories__N` environment variables
- Multipart uploads above 100 MiB
