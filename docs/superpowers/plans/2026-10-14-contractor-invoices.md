# Contractor Invoices & Attachments Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn Costs "actuals" into first-class contractor invoices: a contractor link, a status and dates, GST, a duplicate guard, a cross-project contractor view, and file attachments through Documents.

**Architecture:** This extends `ActualCost` in `PDS.Costs` and adds a sparse `gsi1` on the costs table for contractor queries. The new read surfaces live in `PDS.Contractors.Contracts`, and Contractors never references Costs. Documents gets a `LinkedModule.Invoice` value and a list-by-link filter; the web joins invoices to their attachments.

**Tech Stack:** .NET 10 Minimal APIs, DynamoDB, FluentValidation, React 19 + Mantine 9 + TanStack Query, openapi-typescript.

**Spec:** `docs/superpowers/specs/2026-10-14-contractor-invoices-design.md`

## Global Constraints

- The foundation constraints apply:
  - only `Contracts/` and the Module class are public
  - `pk`/`sk`/`gsi1pk`/`gsi1sk` are string keys
  - errors are ProblemDetails
  - JSON is camelCase with string enums
  - query parameters are plain handler parameters
- **Module rule:** `PDS.Contractors` must not reference `PDS.Costs`, and `PDS.Costs` must not reference `PDS.Documents`. Costs uses only `PDS.Contractors.Contracts` and `PDS.Portfolio.Contracts` types.
- **No new tests** (user directive). Verify with:
  - `dotnet format PDS.slnx`, then `dotnet build PDS.slnx` (0 warnings)
  - in `web/`: `npm run gen:api && npm run typecheck && npm run lint && npm run build`
  - in `infra/`: `npx aws-cdk@2.1143.0 synth -c deployment=example-dev`
  - the manual checks each task names
- Keep `tests/` compiling.
- All amounts are **excl. GST**. GST (`gstAmount`) never enters cost totals or Cashflow.
- Cashflow behaviour is unchanged: an actual's cash month is still its `date`.
- API changes are additive. Paths and existing field names (`reference`, `/actuals`) stay; UI copy says "Invoices & claims" / "invoice".
- Shared wiring files (`Program.cs`, `routes.tsx`, `ProjectDetailPage.tsx`, the layout nav) get minimal one-line edits or none.
- API port 5092, web port 5202. For local runs, use fresh table names so gsi1 exists, e.g. `Tables__costs=ci-costs`.
- Commits are conventional (`feat(costs): ...`) and end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **A contract whose contractor differs from `contractorId`**, including a null contractor with a contract. Expect 400 on `contractorId`, never a saved entry.
2. **A duplicate invoice number from the same contractor**, in a different project, with different case or surrounding spaces. Expect 400 on `reference`. Updating an entry and keeping its own number must NOT trip the check.
3. **Inactive contractor:** create → 400. Updating an existing invoice whose contractor has since been deactivated, without changing the contractor → 200.
4. **Old items without the new attributes** (no `status`, no `contractorId`, no gsi keys). The cost report, Cashflow and the Reporting export still load, with status shown as Received. Totals are identical to before.
5. **An invoice-linked document** opened in the Documents section's edit modal (LinkedRecordSelect). It must not crash on the unknown module and must show the Invoice option. Deleting an invoice leaves its documents listed.

---

### Task 1: Public read surfaces and the Documents link filter

**Files:**
- Create: `src/Modules/PDS.Contractors/Contracts/ContractorSummary.cs`, `src/Modules/PDS.Contractors/Contracts/IContractorQueries.cs`
- Modify: `src/Modules/PDS.Contractors/Contracts/IContractQueries.cs`, `src/Modules/PDS.Contractors/Data/ContractQueries.cs` (it implements both interfaces), `src/Modules/PDS.Contractors/ContractorsModule.cs`
- Modify: `src/Modules/PDS.Documents/Domain/LinkedModule.cs` (add `Invoice`), `src/Modules/PDS.Documents/Features/ListDocuments.cs`

**Interfaces (produces):**
```csharp
namespace PDS.Contractors.Contracts;
public sealed record ContractorSummary(ContractorId Id, string Name, bool IsActive);
public interface IContractorQueries
{
    /// <summary>Summaries for the ids that exist; unknown ids are omitted.</summary>
    Task<IReadOnlyList<ContractorSummary>> GetContractors(IReadOnlyCollection<ContractorId> ids, CancellationToken ct);
}
// added to IContractQueries:
/// <summary>All contracts of the contractor across projects (any status). Empty for an unknown contractor.</summary>
Task<IReadOnlyList<ContractSummary>> GetContractorContracts(ContractorId contractorId, CancellationToken ct);
```
- Register `IContractorQueries` as the same `ContractQueries` singleton: `services.AddSingleton<ContractQueries>(); services.AddSingleton<IContractQueries>(sp => sp.GetRequiredService<ContractQueries>()); services.AddSingleton<IContractorQueries>(sp => sp.GetRequiredService<ContractQueries>());`. A separate small `ContractorQueries` class is also fine.
- `GetContractors` uses `ContractorStore.GetManyAsync`, and returns `[]` for an empty input without calling Dynamo.
- `GetContractorContracts` uses `ContractStore.ListForContractorAsync` and the contractor's name, with the same mapping as `GetProjectContracts`. Extract a private `ToSummary(Contract, string name)`.
- `ListDocuments.Handle` gains `LinkedModule? linkModule, Guid? linkRecordId` handler params, filtering `r.Document.Fields.Link?.Module == linkModule` and `?.RecordId == linkRecordId` when each is given.

- [ ] Step 1: Implement the above.
- [ ] Step 2: `dotnet format PDS.slnx && dotnet build PDS.slnx` → 0 warnings, 0 errors.
- [ ] Step 3: Commit `feat(contractors): contractor and per-contractor contract queries; feat(documents): invoice link type and list filter`.

### Task 2: Costs backend — invoice fields, rules, GSI, report enrichment

**Files:**
- Create: `src/Modules/PDS.Costs/Domain/InvoiceStatus.cs` (`internal enum InvoiceStatus { Received, Approved, Paid, Disputed }`)
- Modify:
  - `Domain/ActualCostFields.cs`, `Domain/CostEntryRules.cs`
  - `Data/CostsTable.cs`, `Data/CostKeys.cs`, `Data/ActualCostItemMapper.cs`, `Data/CostStore.cs`
  - `Features/CostEntryDtos.cs`, `Features/CostEntryValidators.cs`, `Features/CostsProblems.cs`, `Features/CostReferences.cs`
  - `Features/CreateActualCost.cs`, `Features/UpdateActualCost.cs`
  - `Features/GetCostReport.cs`, `Features/CostReportDtos.cs`

**Interfaces (produces, used by Tasks 3–5):**
```csharp
internal sealed record ActualCostFields(
    BudgetLineId BudgetLineId, ActualCostType Type, ContractId? ContractId, ContractorId? ContractorId,
    string? Payee, string Description, string? Reference, DateOnly Date, DateOnly? DueDate,
    InvoiceStatus Status, DateOnly? PaidDate, Money Amount, Money? Gst, string? Notes);

// CostsTable
public const string Gsi1 = "gsi1";
public static readonly TableDefinition Definition = new(LogicalName, [new GlobalIndex(Gsi1, "gsi1pk", "gsi1sk")]);
// CostKeys
public static string ContractorKey(ContractorId id) => $"CONTRACTOR#{id.Value}";
// CostStore
Task<IReadOnlyList<ActualCost>> ListActualsForContractorAsync(ContractorId id, CancellationToken ct); // gsi1, all pages
Task<IReadOnlyList<BudgetLine>> ListLinesAsync(ProjectId projectId, CancellationToken ct);           // pk + begins_with(sk, LINE#)

// DTOs (JSON names camelCase)
ActualCostDetails(..., Guid? ContractorId, ..., DateOnly? DueDate, InvoiceStatus Status, DateOnly? PaidDate, decimal Amount, decimal? GstAmount, ...)
CostReportActual(ActualCostDetails Entry, string BudgetLineName, string? ContractTitle, string? ContractorName)
CostReportContract(..., decimal? ValueAmount, decimal InvoicedToDate)
```
Rules:
- **Mapper.**
  - Write `contractorId`, `status`, `dueDate`, `paidDate` and `gstAmount`, the optional ones only when present.
  - When `ContractorId` is set, also write `gsi1pk = CostKeys.ContractorKey(id)` and `gsi1sk = $"ACTUAL#{date:yyyy-MM-dd}#{id}"`.
  - Read missing `status` as `Received`, and the other missing values as null. GST uses the item `currency`.
- **Normalise.** `PaidDate = Status == Paid ? PaidDate : null`.
- **EnsureValid** additionally checks:
  - Invoice/Claim needs a ContractorId
  - Paid needs a PaidDate
  - `DueDate >= Date`
  - Gst is valid money with a sign not opposite to Amount
- **Validator** (fields nullable on input so omission is a 400):
  - `ContractorId` NotNull when Type is Invoice or Claim ("Choose a contractor.")
  - `Status` NotNull ("Choose a status.") and IsInEnum
  - `PaidDate` NotNull when Status == Paid ("Enter the paid date.")
  - `DueDate >= Date` ("The due date can't be before the invoice date.")
  - `GstAmount` at most 2 dp, within MaxAbs, and not of opposite sign to Amount ("GST can't be the opposite sign to the amount.")
- **CostReferences.CheckActualAsync** (new; keep the existing `CheckAsync` for commitments). Its arguments are:
  - `projectId`, `data`, `budgetLineId`, `contractId`, `contractorId`
  - `ContractorId? existingContractor` (null on create)
  - `Guid? selfId`
  - `IContractQueries`, `IContractorQueries`, `CostStore`, `reference`, `ct`

  The checks, in order:
  1. line exists
  2. contract on project
  3. contractor exists (`ContractorUnknown` → 400 `contractorId` "Choose a contractor.")
  4. contract's contractor == contractorId (`ContractorMismatch` → 400 `contractorId` "The contractor must match the contract's contractor.")
  5. contractor inactive and (create or contractorId != existingContractor) (`ContractorInactive` → 400 `contractorId` "Choose an active contractor.")
  6. a reference present with a contractor: `ListActualsForContractorAsync` contains another entry whose trimmed reference equals it, case-insensitive (`DuplicateInvoiceNumber` → 400 `reference` "This contractor already has an invoice with this number.")
- **Create/Update handlers** inject `IContractorQueries` and call `CheckActualAsync`. Update passes `entry.Fields.ContractorId` and `entry.Id.Value`.
- **GetCostReport:**
  - Resolve contractor names via `IContractorQueries.GetContractors` for the distinct actual contractor ids.
  - The actual's `ContractorName` = the contractor's name, else the contract's contractor name, else null.
  - `CostReportContract.InvoicedToDate` = Σ actual amounts with that ContractId (rounded to 2 dp).
  - Totals logic is untouched.

- [ ] Step 1: Implement.
- [ ] Step 2: Build with 0 warnings.
- [ ] Step 3: Manual check. Run the API with `ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/PDS.Api --urls http://localhost:5092`, with env `Tables__costs=ci-costs` plus fresh names for the other tables (`ci-portfolio`, `ci-contractors`, `ci-documents`, …). Then:
  - create a project, a line, 2 contractors and a contract
  - POST an invoice with a mismatched contractor → 400 `contractorId`
  - POST the same number twice (second time in another project, with different case) → 400 `reference`
  - set Paid without a paid date → 400 `paidDate`
  - deactivate a contractor, then create → 400, and update its existing invoice → 200
- [ ] Step 4: Commit `feat(costs): contractor-linked invoices with status, dates, GST and duplicate guard`.

### Task 3: Contractor invoices endpoint, Reporting columns, OpenAPI regen, docs notes

**Files:**
- Create: `src/Modules/PDS.Costs/Features/GetContractorInvoices.cs`, `src/Modules/PDS.Costs/Features/ContractorInvoicesDtos.cs`
- Modify:
  - `src/Modules/PDS.Costs/CostsModule.cs` (new group `/api/costs/contractors/{contractorId:guid}`, tag Costs, CanRead)
  - `Features/CostEntriesExport.cs`
  - `web/src/shared/api/openapi.json` + `schema.d.ts` (regenerated)
  - `docs/superpowers/specs/2026-10-14-cashflow-design.md` (one-line note)
  - `README.md` (a local note: recreate `pds-costs` for gsi1)

**Interfaces (produces):**
```csharp
internal sealed record ContractorInvoiceTotals(decimal Invoiced, decimal Paid, decimal Outstanding, decimal Disputed, decimal Gst);
internal sealed record ContractorInvoice(ActualCostDetails Entry, Guid ProjectId, string? ProjectCode, string? ProjectName,
    bool ProjectArchived, string BudgetLineName, string? ContractTitle);
internal sealed record ContractorInvoiceContract(Guid ContractId, Guid ProjectId, string? ProjectCode, string? ProjectName,
    string Title, string Status, decimal? ValueAmount, decimal InvoicedToDate, decimal? Remaining);
internal sealed record ContractorInvoices(Guid ContractorId, string ContractorName, bool ContractorActive, string Currency,
    ContractorInvoiceTotals Totals, List<ContractorInvoice> Invoices, List<ContractorInvoiceContract> Contracts);
```
`GET /api/costs/contractors/{contractorId}/invoices`, name `GetContractorInvoices`:
- 404 when `GetContractors([id])` is empty.
- The invoices come from `ListActualsForContractorAsync`, ordered by date descending, then description.
- Projects come from `portfolio.GetSummaries(distinct)`. Line names come from `ListLinesAsync` per distinct project, falling back to "(deleted line)".
- Contracts come from `IContractQueries.GetContractorContracts`, and contract titles come from that map.
- Totals are rounded to 2 dp, with Outstanding = Invoiced − Paid.
- Contracts are ordered by project code, then title; `Remaining = ValueAmount - InvoicedToDate` when the value is present.

Reporting `cost-entries`: add the columns `contractor_id`, `contractor_name`, `status`, `due_date`, `paid_date` and `gst_amount`, placed after `payee` and after `date` as natural. They're null for commitments. `contractor_name` = the contractor lookup (via `IContractorQueries`, one batch for all ids), else the contract's contractor. Update `Title` to "Commitments, invoices and claims".

- [ ] Step 1: Implement.
- [ ] Step 2: Build. `dotnet build PDS.slnx` (Debug) regenerates `web/src/shared/api/openapi.json`. Then run `npm run gen:api`.
- [ ] Step 3: Manual check:
  - `GET /api/costs/contractors/{id}/invoices` lists invoices from two projects, with correct totals and per-contract invoiced-to-date
  - an unknown id → 404
  - `GET /api/reporting/exports/cost-entries` includes the new columns
  - `GET /api/cashflow/projects/{id}` still reconciles
- [ ] Step 4: Commit `feat(costs): contractor invoices endpoint and export columns`.

### Task 4: Web — Invoices & claims tab with attachments

**Files:**
- Modify:
  - `web/src/features/costs/types.ts` (`invoiceStatuses`, `invoiceStatusLabel`, `invoiceStatusColor`, the `ContractorInvoices` type, `actualCostTypeLabel` unchanged)
  - `costEntryFormModel.ts` (new fields: `contractorId`, `status`, `dueDate`, `paidDate`, `gstAmount`; zod rules mirroring Task 2)
  - `CostEntryFormModal.tsx` (Contractor select, Status, Due, Paid date when Paid, GST; contract → contractor default; Payee only when no contractor; label Reference "Invoice / claim no." for actuals; optional `fixedContractorId` and `projectId` props for Task 5)
  - `CostEntriesTab.tsx` (actual columns per spec §5, status badge, Files column)
  - `CostReportPage.tsx` (tab label "Invoices & claims")
  - `api.ts` (`useActiveContractors()` → GET `/api/contractors`; `useContractorInvoices(id)`; invalidate `['contractor-invoices']` in `useRefreshReport`)
  - `web/src/features/documents/api.ts` (`useDocuments(projectId, filter?: { linkModule?: LinkedModule; linkRecordId?: string })` → query key `['documents', projectId, filter]`)
  - `documents/types.ts` (`LinkedModule` adds `'Invoice'`; label "Invoice")
  - `documents/LinkedRecordSelect.tsx` (Invoice group from `/api/costs/projects/{projectId}` actuals, label `joinLabel(contractorName ?? payee, reference ?? description)`)
- Create: `web/src/features/costs/InvoiceAttachments.tsx`, exporting:
  - `useInvoiceAttachments(projectIds: string[])` → `Map<recordId, DocumentDetails[]>` via `useQueries` over `useDocuments`-equivalent queries with `linkModule: 'Invoice'`
  - `<InvoiceFiles projectId docs canAttach invoiceLabel invoiceId />`, which renders one download `Anchor` per document (`useDownload(projectId)`, current version) and an "Attach" button. The button opens a hidden `<input type=file>` picker and calls `useUploadDocument(projectId)` with `{ title: file name minus extension, category: pickInvoiceCategory(settings.categories), link: { module: 'Invoice', recordId, label }, tags: [], notes: null }`. It shows a toast on success or failure, using `handleUploadError`-style messages or `error.message`, and validates size and type with `validateFile` from `documents/format`.
  - `pickInvoiceCategory(categories)`: the case-insensitive "Invoice", else "Other", else `categories[0]`.

- [ ] Step 1: Implement.
- [ ] Step 2: `npm run typecheck && npm run lint && npm run build`.
- [ ] Step 3: Commit `feat(costs): invoices & claims tab with contractor, status and attachments (web)`.

### Task 5: Web — contractor page Invoices section and invoiced-to-date columns

**Files:**
- Create: `web/src/features/costs/ContractorInvoicesSection.tsx`, containing:
  - a Card titled "Invoices"
  - a totals strip of Invoiced, Paid, Outstanding and Disputed
  - a table with Project (link to `/projects/:id/costs`), Date, No., Line, Contract, Due, Status, Amount, GST, Files and Edit
  - "Add invoice", hidden when the contractor is inactive or the user lacks canWrite. It opens a project picker (`/api/portfolio/projects`, non-archived), then `CostEntryFormModal` with kind `actual`, that project's cost report lines and contracts filtered to this contractor, and `fixedContractorId`.
  - Editing an invoice loads its project's cost report the same way.
- Modify:
  - `web/src/features/contractors/ContractorDetailPage.tsx` (render `<ContractorInvoicesSection contractorId contractorName contractorActive />` after ContractsCard; add an "Invoiced" column to ContractsCard from `useContractorInvoices(contractorId).data?.contracts`)
  - `web/src/features/contractors/ProjectContractsSection.tsx` (an "Invoiced" column from `useCostReport(projectId).data?.contracts`)

- [ ] Step 1: Implement.
- [ ] Step 2: `npm run typecheck && npm run lint && npm run build`.
- [ ] Step 3: Commit `feat(costs): contractor invoices section and invoiced-to-date columns (web)`.

### Task 6: End-to-end verification and PR summary

- [ ] Step 1: Run the full verification set from Global Constraints, including `cdk synth`, and confirm the costs table now has gsi1 in the synthesised template.
- [ ] Step 2: Manual API round trip with `Documents:Storage=Local`:
  - request an upload
  - PUT the file to the local URL
  - create the document with `link {module: Invoice}`
  - `ListDocuments?linkModule=Invoice&linkRecordId=` returns it
  - the download URL returns the bytes
- [ ] Step 3: Cost totals check. Write old-shape actual items, with no status or contractor, directly to `ci-costs` using the AWS CLI or a small script against DynamoDB Local. Confirm the report loads and totals equal Σ amounts.
- [ ] Step 4: Browser check on `http://localhost:5202` against API 5092:
  - contractor page → Invoices → Add invoice
  - Attach file
  - Download

  Use Claude in Chrome if it's available, and say so if not.
- [ ] Step 5: Update the PR body with what was built, the evidence, and the rulings. Fix any issues found, with commits.

## Spec coverage

| Spec item | Task |
|---|---|
| ContractorSummary / IContractorQueries / GetContractorContracts | 1 |
| LinkedModule.Invoice + list filter | 1 |
| New fields, rules, duplicate guard, GSI, report enrichment | 2 |
| Contractor invoices endpoint, export columns, Cashflow note | 3 |
| UI rename, form, attachments, LinkedRecordSelect | 4 |
| Contractor page section, invoiced-to-date columns | 5 |
| Manual and browser verification | 6 |
