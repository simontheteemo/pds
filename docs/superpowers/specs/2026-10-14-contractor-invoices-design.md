# PDS Contractor Invoices & Attachments — Design Spec

Status: autonomous design (user unavailable; decisions recorded for later review). Branch `feat/contractor-invoices`.

## 1. Purpose

The user reported that the Contractors module "isn't working functionally". There's no way to track all the invoices from one contractor, which should be a one-to-many relationship, and invoices should be able to have files uploaded to storage.

Costs already records **actuals** of type Invoice, Claim and Other against a budget line, with an optional contract and a free-text payee. This change turns an actual into a first-class invoice:

- a real link to a contractor (one contractor → many invoices across projects)
- an invoice number, a due date, a status, a paid date and an optional GST amount
- a duplicate-number guard
- a cross-project contractor view with totals
- file attachments through the existing Documents module

No new upload pipeline and no new module.

### Stated vs decided

| Stated (user / coordinator) | Decided by Claude (review later) |
|---|---|
| Invoices live in Costs; extend the actual rather than duplicate it | The domain stays `ActualCost`. The API keeps its paths (`/actuals`) and its existing field names, and adds new fields (additive). The **UI** renames the "Actuals" tab and nouns to **"Invoices & claims"** / "invoice". Renaming the API would churn Cashflow, Reporting and the generated types without any user-visible gain |
| `ContractorId?` — required for Invoice and Claim, optional for Other | As stated. `payee` text stays and is used for Other entries with no contractor. The UI shows the Payee field only when no contractor is chosen |
| A given `ContractId` must match the contractor; 400 otherwise | A contract on the entry requires `contractorId` = that contract's contractor. A missing contractor counts as a mismatch: 400 on `contractorId`. The UI defaults the contractor from the chosen contract |
| Inactive contractors can't receive new invoices; mirror Contracts | Unknown contractor → 400 on `contractorId`. Inactive contractor → 400 on `contractorId` on **create**, and on **update when the contractor changes**. An update that keeps an inactive contractor is allowed, so existing invoices stay editable |
| Invoice number: keep `Reference` or rename | **Keep `reference`** (API and storage). The UI labels it "Invoice / claim no." for actuals. Commitments share the field and form model, so a rename would split them for no gain |
| Due date | `dueDate?`. If present, it must be ≥ `date`: 400 "The due date can't be before the invoice date." |
| Status Received / Approved / Paid / Disputed; decide the transitions | `InvoiceStatus` enum. **Any transition is allowed** (record-keeping, as Contracts does). Required on create and update. Old items without a status read as `Received` |
| Paid date, required when Paid | `paidDate` is required when `status = Paid` (400 on `paidDate`). When the status isn't Paid, the server **drops** any paid date it's sent, so moving Paid → Disputed clears it. No ordering rule against `date`, because a prepayment can be paid before the invoice date |
| Optional GST amount; amounts stay excl. GST | `gstAmount?`, with at most 2 dp and within `MoneyRules.MaxAbs`. Its sign can't be opposite to `amount`: a credit note has GST ≤ 0. GST is display and export only and never enters cost totals |
| Duplicate guard: same number + same contractor → 400; per project or global | **Global per contractor**, because invoice numbers are the contractor's own sequence and don't restart per project. The check uses the new GSI with a case-insensitive comparison of the trimmed number, across all entry types, excluding the entry being updated. Entries with no contractor or no number aren't checked. Accepted gap: two simultaneous saves of the same number can both pass, since the GSI read is eventually consistent. Single-tenant, low concurrency |
| GSI on the Costs table | `gsi1` (`gsi1pk`/`gsi1sk`, projection ALL, as Contractors and Portfolio do). Only actual items with a contractor carry it (a sparse index): `gsi1pk = CONTRACTOR#<contractorId>`, `gsi1sk = ACTUAL#<yyyy-MM-dd>#<id>`. CDK and local bootstrap both read `CostsTable.Definition`, so infra changes automatically |
| `GET /api/costs/contractors/{contractorId}/invoices` with totals and per-contract invoiced-to-date | As stated (§4). Totals are `invoiced` (Σ amount), `paid` (Σ where Paid), `outstanding` = invoiced − paid (includes Disputed), and `disputed` (Σ where Disputed). All excl. GST. Per contract: `valueAmount`, `invoicedToDate`, `remaining` (= value − invoiced, null with no value) |
| Contractors must not reference Costs | Respected. New public read surface in `PDS.Contractors.Contracts`: `IContractorQueries.GetContractors(ids)` → `ContractorSummary(Id, Name, IsActive)`, and `IContractQueries.GetContractorContracts(contractorId)`. The contractor page embeds a Costs-owned web component, `features/costs/ContractorInvoicesSection` |
| Attachments via Documents; add a link type | `LinkedModule.Invoice` (the API value is `"Invoice"`, and the record id is the actual's id). "Attach file" uploads through the existing presigned flow into the configured category matching "Invoice", case-insensitive. Fallbacks, in order: "Other", then the first configured category. Title = the file name without its extension. The link label is `<contractor> · <number>`. Downloads use the existing download endpoint |
| Documents may need to list by link | `ListDocuments` gains optional `linkModule` and `linkRecordId` query filters, **per project**. The project cost report needs one call (`linkModule=Invoice`). The contractor page makes one call per distinct project in its invoice list, which is few. A global by-link index isn't worth a new GSI |
| What happens to attachments when an invoice is deleted | **Documents stay**, still linked to the now-missing record. They remain in the project's Documents section with their snapshot label, and can be deleted there. Silently deleting files is worse than an orphan link, and Documents already treats links as soft references (its spec §1) |
| Reporting `cost-entries` new columns | Adds `contractor_id`, `contractor_name` (the contractor's name, falling back to the contract's contractor), `status`, `due_date`, `paid_date` and `gst_amount`. All blank for commitments. The column `reference` is unchanged |
| Cashflow: invoice date vs paid date | **Unchanged: an actual's cash-out month is still its invoice `date`.** Switching to `paidDate` would leave unpaid invoices undated, needing a due-date or invoice-date fallback. It would also move historic costs between months for the same report, and it changes Cashflow's documented behaviour. Reconciliation (Σ outflows = Actual + FTC) is unaffected. A one-line note in the Cashflow spec records `paidDate ?? dueDate ?? date` as the obvious future refinement |
| Invoiced to date on contract rows (nice to have) | Included, because it's cheap. The cost report's `contracts[]` gains `invoicedToDate`, and the project page's Contracts section shows it from the already-loaded cost report query. On the contractor page, the Contracts card shows it from the contractor-invoices query. Both are web-side reads; Contractors stays unaware of Costs |
| Old items lacking new fields must not crash | The mapper defaults: `status` → Received, the others → null. Old Invoice/Claim items without a contractor still load and display. They must gain a contractor on their next edit, and the UI pre-fills it from the contract when there is one |
| No new tests | Verify by build, typecheck, lint, synth, manual API checks and a browser check |

### Success criteria
- On the cost report, the **Invoices & claims** tab shows each entry's contractor, number, dates, status badge, amount and GST, plus its attachments. Each attachment downloads, and an **Attach file** action uploads one.
- On the contractor detail page, an **Invoices** section shows the contractor's invoices across all projects, with project, totals and attachments. It also has an **Add invoice** action (pick a project, then a line and an optional contract).
- The Contracts cards show **Invoiced to date**.
- The API returns 400 for: a contractor/contract mismatch, a duplicate number per contractor, an inactive contractor on a new invoice, and Paid without a paid date.
- The cost report totals for existing data are unchanged, because GST and status never enter the totals.

## 2. Architecture

- `PDS.Contractors.Contracts` (public) gains:
  - `ContractorSummary(ContractorId Id, string Name, bool IsActive)`
  - `IContractorQueries.GetContractors(IReadOnlyCollection<ContractorId>)`, which omits unknown ids
  - `IContractQueries.GetContractorContracts(ContractorId)`, which uses the existing gsi1
- `PDS.Costs` uses only those Contracts types (no new project reference). It owns the new GSI and the new endpoint.
- `PDS.Documents` adds an enum value and a list filter. Costs doesn't reference Documents: the **web** joins invoices to their attachments.
- Web:
  - `features/costs` imports hooks from `features/documents/api` (upload, list, download) instead of re-implementing the upload. This is a new cross-feature import direction (costs → documents), recorded as a ruling.
  - `features/contractors` imports `useCostReport` / `useContractorInvoices` from `features/costs/api` for the invoiced-to-date columns. It also embeds `ContractorInvoicesSection`, as `ProjectDetailPage` embeds `ProjectCostsCard`.

## 3. Data

`ActualCostFields` adds: `ContractorId? ContractorId`, `InvoiceStatus Status`, `DateOnly? DueDate`, `DateOnly? PaidDate`, `Money? Gst`.

The actual item gains these attributes:
- `contractorId` (S, optional)
- `status` (S)
- `dueDate` (S date, optional)
- `paidDate` (S date, optional)
- `gstAmount` (N, optional)
- `gsi1pk`/`gsi1sk`, set only when a contractor is present

The table definition becomes `new TableDefinition("costs", [new GlobalIndex("gsi1", "gsi1pk", "gsi1sk")])`.

Normalisation: `PaidDate = Status == Paid ? PaidDate : null`. Domain `EnsureValid` also guards:
- a contractor for Invoice/Claim
- a paid date when Paid
- due ≥ date
- the GST sign

## 4. API

Existing endpoints (additive fields):
- `POST /actuals` and `PUT /actuals/{id}` take `contractorId`, `status`, `dueDate`, `paidDate` and `gstAmount`.
- `ActualCostDetails` returns them too.
- `CostReportActual` gains `contractorName`, now resolved from the contractor and falling back to the contract's contractor.
- `CostReportContract` gains `invoicedToDate`.

Check order on create/update:
1. project (404 / archived 400)
2. line and contract (existing)
3. contractor exists (400 `contractorId`)
4. contract ↔ contractor match (400 `contractorId`)
5. inactive contractor (create, or a changed contractor; 400 `contractorId`)
6. duplicate number (400 `reference`)
7. version (409)

New endpoint: `GET /api/costs/contractors/{contractorId:guid}/invoices` (CanRead). It returns 404 for an unknown contractor.

```
ContractorInvoices {
  contractorId, contractorName, contractorActive, currency,
  totals { invoiced, paid, outstanding, disputed, gst },
  invoices: [{ entry: ActualCostDetails, projectId, projectCode, projectName, projectArchived,
               budgetLineName, contractTitle }],      // newest date first
  contracts: [{ contractId, projectId, projectCode, projectName, title, status,
                valueAmount, invoicedToDate, remaining }]
}
```

The endpoint reads the invoices from gsi1, the projects from Portfolio, the line names with one lines-only query per distinct project, and the contracts with `GetContractorContracts`.

Documents: `GET /api/documents/projects/{projectId}/documents?linkModule=Invoice&linkRecordId=<guid>`. Both filters are optional and combine with AND.

## 5. UI

- **Cost report → "Invoices & claims" tab** (it was Actuals). The columns are:
  - Date
  - Contractor / payee
  - No.
  - Line
  - Contract
  - Due
  - Status badge (Received gray, Approved blue, Paid green, Disputed red)
  - Amount
  - GST
  - Files (a download link per file, plus an "Attach" button)
  - actions

  The form adds a Contractor select (active contractors, plus the current one if it's inactive), Status, Due date, Paid date (shown when Paid) and GST. Choosing a contract sets the contractor.
- **Contractor detail page → Invoices section** (`features/costs/ContractorInvoicesSection`). It has:
  - a totals strip (Invoiced, Paid, Outstanding, Disputed)
  - an invoice table with Project, Date, No., Line, Due, Status, Amount, GST and Files
  - "Add invoice", which opens the same form with the contractor fixed and a Project select first; the form then loads that project's cost report for its lines and contracts
  - Edit, attach and download per row
  - no Add button for an inactive contractor
- **Contracts cards**: an "Invoiced" column on the contractor page and the project page.
- **Documents**:
  - `LinkedModule` includes Invoice.
  - The linked-record select lists the project's invoices from the cost report, so an invoice-linked document can be edited without crashing.

## 6. Out of scope
- Approval workflow or permissions per status.
- Retention calculations.
- Payment runs.
- GST-inclusive totals.
- OCR of invoice files.
- Deleting attachments together with the invoice.
- A global uniqueness lock on invoice numbers (the GSI check is best-effort, §1).

## 7. Risks
- **Duplicate-check race:** the GSI is eventually consistent. This is accepted.
- **Old Invoice/Claim items without a contractor** are blocked from saving until a contractor is chosen. That's intended, and it's local data only.
- **Shared DynamoDB Local:** the existing `pds-costs` table there was created without gsi1 (the bootstrap only creates missing tables). Manual checks run against a fresh table name (`Tables__costs=ci-costs`). Other developers must recreate their local `pds-costs` table, which the README notes.
