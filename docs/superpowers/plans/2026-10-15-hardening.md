# Hardening pass — implementation plan

**Goal:** Fix reliability and user-visible issues found during the module build. This is a fix list, not a feature, so there's no spec. The coordinator's fix list (quoted in each task) is the authority.

**Branch:** `chore/hardening`. Worktree `/Users/simondai/Code/pds/.worktrees/hardening`. API port 5089, web port 5199.

## Global Constraints

- **Do NOT touch `src/Modules/PDS.Consents` or `web/src/features/consents`.** A parallel branch owns them.
- No new tests, and existing tests must keep compiling. Verify with these commands:
  - `dotnet format PDS.slnx`, then `dotnet build PDS.slnx` with 0 warnings. A Debug build regenerates `web/src/shared/api/openapi.json`.
  - In `web/`: `npm run gen:api && npm run typecheck && npm run lint && npm run build`.
- No new packages or frameworks.
- Keep changes to shared code (`web/src/shared`, `PDS.Shared`) small and additive.
- One commit per task, in conventional style (`fix(<area>): ...`), ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Keep the source generic, with no client names.

## Review Focus

- Is caller cancellation preserved, so that a TanStack Query abort still rejects with the original AbortError and not a timeout ApiError?
- Do timers leak? Is the timeout cleared once a response arrives?
- Do server status codes and ProblemDetails match the brief (413 + ProblemDetails)?
- Is the Documents create concurrency correct under a race: one document, the loser cleans up its copied file, and replays return the same document?
- Are there behaviour regressions in the touched UI?

---

### Task 1: API request timeout (web)

Fix list: "`web/src/shared/api/client.ts` has no timeout, so a stalled request spins forever. Add an AbortController-based timeout of about 30 s, configurable as a constant. Surface it as a normal `ApiError`-style failure with a clear message, so existing toasts show 'The server took too long to respond'. Keep caller-supplied abort signals working, e.g. from TanStack Query."

**File:** `web/src/shared/api/client.ts`. This is the only file. Every call goes through `createApi`, which uses openapi-fetch's `createClient`.

1. Add `export const REQUEST_TIMEOUT_MS = 30_000;`.
2. Add a `fetch` implementation and pass it as `createClient<paths>({ baseUrl, fetch: fetchWithTimeout })`. openapi-fetch 0.17 calls `fetch(request: Request)` after middleware, and the caller's `signal` is already on `request.signal`. The implementation must:
   - create an `AbortController` for the timeout
   - link `request.signal` to it **manually**:
     - if it's already aborted, abort now
     - otherwise `addEventListener('abort', ..., { once: true })` and remove the listener in `finally`
     - Don't use `AbortSignal.any`. The browserslist/Vite default target (baseline-widely-available, about Chrome 107 / Safari 16) predates it (Chrome 116 / Safari 17.4).
   - `setTimeout(() => { timedOut = true; controller.abort(); }, REQUEST_TIMEOUT_MS)` and `clearTimeout` in `finally`, which runs once response headers arrive
   - call `globalThis.fetch(new Request(request, { signal: controller.signal }))`
   - on rejection:
     - if `timedOut`, throw `new ApiError(408, { title: 'The server took too long to respond', detail: 'Check your connection and try again.', status: 408 })`
     - otherwise rethrow the original error unchanged, so caller aborts stay AbortErrors
3. Status 408 is deliberate. `App.tsx`'s retry predicate does not retry ApiErrors below 500, so a timed-out query isn't retried twice more (90 s of spinner). Generic toasts use `error.message`, which is the title.
4. Add a short doc comment that the timeout covers time-to-headers only, not body streaming.

Verify: run the standard web checks. For a manual check, run Vite on port 5199 against a stub that never responds (or temporarily lower the constant) and confirm the toast or error. Record the evidence in the report.

### Task 2: Reporting export size limit (413)

Fix list: "Lambda buffered responses fail above 6 MB, which gives a 500 today. Measure the generated CSV. If it's over a safe threshold, about 5.5 MB, as a constant, return a ProblemDetails 413 instead. The message should say the export is too large and suggest contacting an administrator. Log it. Make the web Reports page show that message. Don't build an S3 offload."

1. `src/Modules/PDS.Reporting/Features/DownloadExport.cs`:
   - Add `internal const int MaxExportBytes = 5_500_000;` with a comment: API Gateway/Lambda buffered response limit is 6 MB including JSON-envelope escaping, so headroom is kept.
   - After `CsvWriter.Write`, if `bytes.Length > MaxExportBytes`:
     - log a **warning** with the dataset, size in bytes, row count and user
     - return `TypedResults.Problem(title: "This export is too large to download.", detail: "The file would be larger than the download limit. Contact an administrator to get this data.", statusCode: StatusCodes.Status413PayloadTooLarge)`
   - The return type becomes `Results<FileContentHttpResult, NotFound, ProblemHttpResult>`. Add `.ProducesProblem(StatusCodes.Status413PayloadTooLarge)` to the route.
2. `web/src/features/reporting/ReportsPage.tsx`: change the toast message to prefer `err.problem?.detail`, then `err.problem?.title`, then `err.message`. The title stays "Download failed". For a 413, use the problem title as the toast title, so the user sees "This export is too large to download." with the detail as the message.
3. Regenerate `openapi.json` by building, then run `npm run gen:api`. Commit both.
4. **GST wording** (added by the coordinator). The Reports page header says all amounts "exclude GST". That's wrong for the Units and sales export, because Cashflow treats sale prices as GST-inclusive, as recorded in Sales.
   - In `web/src/features/reporting/ReportsPage.tsx`, change the header sentence to: "Amounts are in the deployment currency. Costs exclude GST; sale prices are as recorded in Sales (assumed GST-inclusive)."
   - In `src/Modules/PDS.Sales/Features/UnitsExport.cs`, change the Description to: "Every unit or lot with its listing details and current sale: status, buyer, price (as recorded, assumed GST-inclusive) and key dates."
   - Leave the Costs export descriptions (`excl. GST`) as they are.

Verify: run the build. For a manual check, temporarily lower the constant locally (don't commit that), run the API on port 5089, and `curl` an export to see the 413 ProblemDetails. Put the evidence in the report.

### Task 3: Contractors polish

Fix list: "the contractor/contract option label shows 'Name — ' or ' — title' when trade or contractor name is missing; omit the separator instead. Trim the NZBN on the server before validating. De-duplicate the NZBN regex. De-duplicate the Active/Inactive badge logic between list and detail pages."

1. Add a tiny additive helper to `web/src/shared/format.ts`: `export function joinLabel(...parts: (string | null | undefined)[]): string`. It joins the trimmed non-empty parts with `' — '`.
   - Use it in `web/src/features/contractors/ContractFormModal.tsx` (contractor option: name and trade).
   - Use it in `web/src/features/costs/CostEntryFormModal.tsx` (contract option: contractorName and title).
   - Use it in `web/src/features/costs/CostEntriesTab.tsx` (Contract column: contractorName and title; keep `'—'` when there's no contract).
   - Check `web/src/features/documents/LinkedRecordSelect.tsx` for a similar `title · contractorName` join that can show a dangling separator. If there is one, fix it in the same spirit, keeping its `·` separator. `joinLabel` may take an optional separator.
2. Server, `src/Modules/PDS.Contractors/Features/ContractorValidators.cs`: change the NZBN rule to validate the trimmed value against `ContractorRules.NzbnPattern()`, removing the duplicated literal regex. For example, `RuleFor(x => x.Nzbn).Must(v => ContractorRules.NzbnPattern().IsMatch(v!.Trim()))...When(not blank)`. `ContractorRules.Normalise` already trims before persisting.
3. Web: create `web/src/features/contractors/ActiveBadge.tsx` exporting `ActiveBadge({ active }: { active: boolean })`. It renders an outline green "Active" or an outline gray "Inactive". Use it in `ContractorsPage.tsx`, replacing the inline ternary, and in `ContractorDetailPage.tsx`, replacing `{!c.isActive && <Badge color="gray">Inactive</Badge>}`. The detail page now always shows the status badge. Remove imports that become unused.

### Task 4: Costs polish

Fix list items for Costs:

1. **Filter-aware empty state:** in `web/src/features/costs/CostEntriesTab.tsx`, when `rows.length > 0` but the filtered list is empty, show "No commitments on this budget line." or "No actuals on this budget line." with a subtle "Show all lines" button that clears the filter. Keep "No commitments recorded." / "No actuals recorded." for a truly empty list. The Select stays rendered and usable.
2. **Stale filter:** if `lineFilter` is no longer in `report.lines`, for example because the line was deleted, reset it. Derive an effective filter (`null` when unknown) and use it for filtering, for the Select value and for `defaultLineId`. Also reset the state, either in a `useEffect` or by setting it during render per React guidance.
3. **Validation message:** in `src/Modules/PDS.Costs/Features/BudgetLineValidators.cs` and `CostEntryValidators.cs`, check "too large" (`MoneyRules.MaxAbs`) **before** `PrecisionScale`. A huge number then reports "… is too large." and not "Use at most 2 decimal places.". The rules keep `Cascade(CascadeMode.Stop)`.
4. **Location headers:** `CreateBudgetLine.cs`, `CreateCommitment.cs` and `CreateActualCost.cs` currently return `Created("/api/costs/projects/{projectId}/lines|commitments|actuals/{id}")`, but those GET routes don't exist. Point all three at the existing cost report route `/api/costs/projects/{projectId}` (`GetCostReport`).
5. **Delete-error toast:** extract the duplicated `onError` toast in `BudgetTab.tsx` and `CostEntriesTab.tsx` into `web/src/features/costs/deleteErrorToast.ts`, as `showDeleteError(error: Error, noun: string)`. It keeps the 409, 400 and generic branches identical to today, and both tabs use it.

### Task 5: Sales + Documents

1. **Sales:** in `web/src/features/sales/UnitFormModal.tsx`, add `allowNegative: false` to `roomProps`. Also check `SaleFormModal.tsx` and `UnitFormModal`'s other count inputs, and add it where a negative value is never valid.
2. **Documents idempotent create:** a concurrent double-confirm of the same `uploadId` creates two documents, because each request mints its own `DocumentId`. Fix it in `src/Modules/PDS.Documents`:
   - Add an upload-claim item to the documents table: `pk = PROJECT#{projectId}`, `sk = UPLOAD#{uploadId}`, `itemType = "UploadClaim"`, `documentId = <id>`. Put it in a new small mapper, or as a section of `DocumentItemMapper`. It never matches the `DOC#` prefix queries, and `Group` ignores unknown item types.
   - In `DocumentStore.CreateAsync`, take the `uploadId`, add a third `TransactWriteItem` that Puts the claim with `attribute_not_exists(pk)`, and return a result:
     - on `TransactionCanceledException` where the claim's cancellation reason is `ConditionalCheckFailed`, return "already claimed"
     - otherwise rethrow
   - Add `DocumentStore.FindByUploadAsync(ProjectId, Guid uploadId)`, which uses a consistent GetItem on the claim and returns the claimed `DocumentId?`.
   - In `CreateDocument.Handle`:
     - **before** `ConfirmAsync`, if a claim exists and its document exists, return `Created` with that document. This is a replay, for example after the first request already deleted the pending object.
     - if the transaction loses the race, best-effort delete the loser's copied file (`upload.Version` storage key, via `storage.DeleteAsync`, with exceptions logged and not thrown), then load the winner via the claim and return `Created` with it
     - if the claim points nowhere, which shouldn't happen, return `DocumentsProblems.UploadMissing()`
   - `AddVersion` is already protected by its expected-version transaction, so leave it unchanged.
   - Check the local DynamoDB table setup and infra for nothing needed. The table has just pk/sk, so no new index is needed.

Verify (manual): with the API on port 5089 and DynamoDB Local, fire two concurrent `POST /api/documents/projects/{id}/documents` requests with the same uploadId, using local storage mode and the dev auth bypass that the README describes. Confirm one document is listed and both responses return the same id. Record the evidence.

### Task 6: Final verification + PR

Run the full verify (format, build, web checks, `cdk synth` per the README/CI). Open a non-draft PR to main titled "chore: hardening pass". Include the evidence, the v2 S3-offload note for exports, and the footer.
