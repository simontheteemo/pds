# PDS Cost Tracking (v1) — Design Spec

- Date: 2026-10-14
- Author: @simon (with Claude)
- Status: **Proceeding on the user's "keep going, verify later" instruction.** Every decision below was made by Claude and is open to review.
- Builds on: [Foundation spec](2026-09-29-foundation-design.md) (roadmap item 3, "Cost Tracking (running totals)"; Portfolio stores only the budget) · [Contractors spec](2026-09-30-contractors-design.md) (cost lines point at contracts)

## 1. Purpose

Replace the per-project cost spreadsheet with a running cost report. For each project, the report shows:
- the budget, broken into lines (land, fees, construction and so on)
- what has been committed (contracts, orders, variations)
- what has actually been invoiced or claimed
- the forecast final cost, forecast to complete, and the variance against budget

The report is shown per line and for the whole project.

### Stated vs decided

| Stated (roadmap / coordinator brief) | Decided by Claude (review later) |
|---|---|
| Budget lines and cost categories per project; generic and configurable | **A budget line *is* the category.** There is no separate category entity. Each project has its own lines (name, sort order, budget). An "Add standard lines" action seeds a default set from configuration (`Costs:StandardLines`). The code default is: Land & acquisition, Professional fees, Consents & levies, Construction, Finance, Marketing & sales, Contingency |
| Commitments (contract values, variations) | A **commitment** belongs to one budget line. It has a type (`Contract`, `Variation`, `Order`) and an optional link to a contract from the Contractors module. Its amount is **copied** in, not live-linked to the contract value. A contract can be split across several lines |
| Actuals and invoices/claims with optional links to a contract | An **actual** belongs to one budget line. It has a type (`Invoice`, `Claim`, `Other`), a date, a payee, a reference, and an optional link to a contract |
| Running totals: budget vs committed vs actual vs forecast-to-complete, with variance | **Computed on read** from one DynamoDB query per project, with no stored counters. The formulas are in §3.3 |
| Use `PDS.Shared.Money` patterns | Every amount is a `Money` in the deployment currency (`Locale:Currency`), stored as `<name>Amount` (N) + `currency` (S), with at most 2 decimal places |
| — | Amounts **exclude GST**. That's the norm for NZ development cost reports, and it's stated in the UI |
| — | Negative amounts: allowed on a `Variation` commitment (an omission) and on an actual (a credit note). Rejected on budgets and on `Contract`/`Order` commitments |
| — | Portfolio's single `Budget` field is left alone. The cost report's budget is the sum of its lines. The two aren't reconciled in v1 (that would need a Portfolio contracts change) |
| — | Commitments and actuals can be edited (CanWrite) and deleted (CanAdminister). A budget line can be deleted (CanAdminister) only when no commitment or actual references it |
| — | A linked contract must belong to the same project. Contract status isn't checked, so a Terminated contract can still carry costs |
| — | Writes on an archived project → 400 on `projectId`. Reads still work |
| Contractors' public surface has no query interface | **Addition to `PDS.Contractors` (Task 1 of this plan):** `IContractQueries.GetProjectContracts(ProjectId)` → `ContractSummary[]`. See §2.1 |
| No new tests (standing directive) | Verify by build, typecheck, lint, synth and manual API checks |

### Success criteria
- On the project detail page, a **Costs** card shows the five headline totals and links to the cost report.
- The **Cost report page** (`/projects/:id/costs`) has:
  - a totals strip
  - a **Budget** tab: per-line table with Budget, Committed, Actual, Forecast final cost, Forecast to complete and Variance, plus a totals row
  - a **Commitments** tab and an **Actuals** tab: tables with a budget-line filter, and add, edit and delete actions
- One new module, `PDS.Costs`, owns one table, `costs`. It uses only `PDS.Portfolio.Contracts` and `PDS.Contractors.Contracts`.

## 2. Architecture

- A new module, `src/Modules/PDS.Costs`, with the same shape as Risks:
  - `Contracts/` (public) holds `BudgetLineId`, `CommitmentId` and `ActualCostId`.
  - Everything else is internal.
  - `CostsModule` handles registration.
- It references `PDS.Portfolio` (Contracts only: `ProjectId`, `IPortfolioQueries.GetSummaries`) and `PDS.Contractors` (Contracts only: `ContractId`, `IContractQueries`, `ContractSummary`).
- Host: one line each, `AddCostsModule()` and `MapCostsEndpoints()`. Infra: `.. CostsModule.Tables`.
- Web: a new `web/src/features/costs` feature and one route, `projects/:id/costs`. `ProjectDetailPage` renders `<ProjectCostsCard>`. There's no new nav item, because costs are always per project.

### 2.1 Required addition to `PDS.Contractors` (public Contracts)

The contractors plan exposes only `ContractorId` and `ContractId`. Costs needs to do two things:
- check that a contract exists and belongs to the project
- show contract titles and contractor names

So Task 1 adds the following to `PDS.Contractors`:

```csharp
// Contracts/ContractSummary.cs
public sealed record ContractSummary(
    ContractId Id, ProjectId ProjectId, ContractorId ContractorId, string ContractorName,
    string Title, string? Reference, string Status, decimal? ValueAmount, string? Currency);

// Contracts/IContractQueries.cs
public interface IContractQueries
{
    /// <summary>All contracts on the project (any status), with contractor names. Empty for an unknown project.</summary>
    Task<IReadOnlyList<ContractSummary>> GetProjectContracts(ProjectId projectId, CancellationToken ct);
}
```

`Data/ContractQueries.cs` implements it with `ContractStore.ListForProjectAsync` plus `ContractorStore.GetManyAsync`. A missing contractor shows as "(unknown contractor)". `AddContractorsModule` registers it as a singleton. This is additive; it changes no existing contractors behaviour.

A single per-project call covers both the membership check and the display names. The cost report loads it once.

## 3. Data

### 3.1 Entities

**BudgetLine**

| Field | Rules |
|---|---|
| `Id` | `BudgetLineId`, UUIDv7 |
| `ProjectId` | |
| `Name` | Required, ≤ 100, unique per project (case-insensitive, trimmed) |
| `SortOrder` | int 0–9999; the list is ordered by SortOrder, then Name |
| `Budget` | Money, ≥ 0 |
| `ForecastRemaining` | Money?, ≥ 0. An optional manual override of the cost still to come beyond what's committed (§3.3) |
| `Notes` | ≤ 2000 |
| Other | Audit fields, `Version` |

**Commitment**

| Field | Rules |
|---|---|
| `Id` | `CommitmentId` |
| `ProjectId`, `BudgetLineId` | The line must exist in the project |
| `Type` | `Contract`, `Variation` or `Order` |
| `ContractId` | Optional; must be one of the project's contracts |
| `Description` | Required, ≤ 200 |
| `Reference` | ≤ 50 (e.g. a PO or variation number) |
| `Date` | DateOnly, required |
| `Amount` | Money. ≥ 0 unless Type = Variation |
| `Notes` | ≤ 2000 |
| Other | Audit fields, `Version` |

**ActualCost**

| Field | Rules |
|---|---|
| `Id` | `ActualCostId` |
| `ProjectId`, `BudgetLineId` | The line must exist in the project |
| `Type` | `Invoice`, `Claim` or `Other` |
| `ContractId` | Optional; must be one of the project's contracts |
| `Payee` | ≤ 200 |
| `Description` | Required, ≤ 200 |
| `Reference` | ≤ 50 (an invoice or claim number) |
| `Date` | DateOnly, required (the invoice or claim date) |
| `Amount` | Money; any sign (negative = credit note), ≠ 0 |
| `Notes` | ≤ 2000 |
| Other | Audit fields, `Version` |

Amounts are bounded at ±999,999,999.99 to keep sums well inside decimal range and to catch typos.

### 3.2 Table `costs`

`pk`/`sk` only, with **no GSI**. A contract-centred view can add one later, since DynamoDB allows a GSI to be added to an existing table.

| Item | pk | sk |
|---|---|---|
| BudgetLine | `PROJECT#<projectId>` | `LINE#<id>` |
| Commitment | `PROJECT#<projectId>` | `COMMIT#<id>` |
| ActualCost | `PROJECT#<projectId>` | `ACTUAL#<id>` |

| Access pattern | Operation |
|---|---|
| The whole project's cost data | Query `pk = PROJECT#<id>` (all pages, consistent read), split by sk prefix |
| Get one item | GetItem |
| Create | PutItem `attribute_not_exists(pk)` |
| Update | PutItem `#version = :expected` (false → 409) |
| Delete | DeleteItem `#version = :expected` (false → 409) |
| Line name uniqueness | Checked in the handler against the project's lines. There's no guard item: a rare race between two simultaneous creates with the same name is accepted (the cost if that's wrong: a duplicate-named line the user can rename) |
| Line in use (delete) | Checked in the handler against the project's commitments and actuals |

### 3.3 Running totals (computed on read)

For each budget line L:

```
Budget            = L.Budget
Committed         = Σ commitment amounts on L            (variations may be negative)
Actual            = Σ actual amounts on L
Exposure          = max(Committed, Actual)               (an invoice with no commitment still counts as spent)
Remaining         = L.ForecastRemaining ?? max(Budget − Exposure, 0)
ForecastFinalCost = Exposure + Remaining
ForecastToComplete= ForecastFinalCost − Actual
Variance          = Budget − ForecastFinalCost           (positive = under budget, negative = overrun)
```

- With no override, `ForecastFinalCost = max(Budget, Committed, Actual)`. The working assumption is that the budget will be fully spent unless commitments or actuals already exceed it.
- An override lets the user say "only $X more to come" (to release savings) or "$Y more than budget still to come" (to show an overrun).
- **Project totals** are the sums of the per-line figures. They're not recomputed from project-wide sums, so the lines always add up to the total.
- An "Unallocated" line isn't needed, because every commitment and actual must reference a line.
- All arithmetic is in `decimal`. No rounding is needed: the inputs have 2 dp and the operations are only sums and max.

## 4. API

Group `/api/costs/projects/{projectId:guid}` (tags "Costs", CanRead). All writes need CanWrite, except deletes, which need CanAdminister. Every route returns 404 for an unknown project. Every write returns 400 on `projectId` for an archived project.

| Method | Route | Behaviour |
|---|---|---|
| GET | `` | `ProjectCostReport`: `{ projectId, currency, totals, lines[], commitments[], actuals[], contracts[] }`. `lines[]` holds each line's fields plus its computed figures. Commitments and actuals carry `budgetLineName`, `contractTitle` and `contractorName` (null when there's no link, or "(unknown contract)" when the linked contract no longer exists). `contracts[]` is the project's `ContractSummary` list, used for the pickers |
| POST | `/lines` | Create a line. 400 `name` if it's a duplicate. → 201 `BudgetLineDetails` |
| POST | `/lines/standard` | Adds each configured standard line whose name isn't already used (budget 0, SortOrder 10, 20, …, continuing after the current maximum). → 200 `BudgetLineDetails[]` (only those added) |
| PUT | `/lines/{lineId}` | Body includes `version`. 400 `name` if it's a duplicate; 409 if stale |
| DELETE | `/lines/{lineId}?version=n` | CanAdminister. 400 `budgetLineId` "This line has commitments or actuals." when in use; 409 if stale → 204 |
| POST | `/commitments` | 400 `budgetLineId` if it's unknown in the project; 400 `contractId` if it's not a contract of this project; 400 `amount` if negative for a non-Variation → 201 |
| PUT | `/commitments/{id}` | Same checks; `version`; 409 |
| DELETE | `/commitments/{id}?version=n` | CanAdminister → 204 / 404 / 409 |
| POST / PUT / DELETE | `/actuals`, `/actuals/{id}` | Same pattern as commitments. 400 `amount` when 0 |

- The line, commitment and actual DTOs use `decimal` amounts with the report's single `currency`, matching how the Portfolio and Contracts DTOs expose `budgetAmount` and `valueAmount`.
- Deletes take `version` as a query parameter, because DELETE request bodies are poorly supported by clients.

## 5. UI

- **`ProjectCostsCard`** (on the project detail page):
  - five figures: Budget, Committed, Actual, Forecast final cost, Variance (red when negative)
  - a "Open cost report" link
  - when there are no lines: "No budget lines yet." and the same link
- **Cost report page** (`/projects/:id/costs`):
  - **Header:** a breadcrumb back to the project (`code · name`) and the title "Cost report". A note says "Amounts exclude GST."
  - **Totals strip:** the same five figures plus Forecast to complete.
  - **Tabs:**
    - **Budget:** a table with columns Line, Budget, Committed, Actual, Forecast final cost, Forecast to complete, Variance, ending in a totals row.
      - A badge ("manual") marks a line with a ForecastRemaining override.
      - Actions: "Add line" and "Add standard lines" (canWrite and not archived), Edit per row (canWrite), Delete per row (canAdminister, with a confirm).
    - **Commitments:** a budget-line filter Select and a table with columns Date, Line, Type, Description, Reference, Contract (`contractorName — title`), Amount, plus a filtered total.
      - Actions: Add, Edit, Delete (by role).
    - **Actuals:** the same, with a Payee column.
  - **Modals:**
    - The line modal has Name, Sort order, Budget, Forecast remaining (optional, with help text explaining the default), and Notes.
    - The commitment and actual modals have a Budget line Select (required), Type, an optional Contract Select (clearable, data from the report's `contracts[]`, label `contractorName — title`), Description, Reference, Date, Amount (negative allowed per type) and Notes. Choosing a contract prefills the Description (if empty) with its title, and prefills the Amount (only for a `Contract` commitment when Amount is empty) with the contract value.
  - **Errors:** 400 errors map onto fields, and unmapped keys go to the top alert (the shared mutation-error helper). A 409 shows "changed by someone else — reload".
- Money uses `formatMoney` from `shared/format`. Viewers see everything read-only.

## 6. Out of scope (v1)
- Reconciliation with Portfolio's `Budget`
- Budget revisions or a history of the approved budget
- Pending vs approved variations, and the retention ledger
- Payment status (paid / unpaid), GST, and the accounting-system integration
- Cashflow (phasing over time), which is roadmap item 6
- Cost totals per contract on the contractor pages
- A portfolio-wide cost roll-up across projects
- Importing from spreadsheets

## 7. Risks
| # | Risk | Handling |
|---|---|---|
| 1 | One query per report reads every item in the project | Fine for thousands of items per project (< 1 MB of pages). Revisit with stored line totals if reports get slow |
| 2 | The contractors `IContractQueries` addition conflicts with that branch | It's additive (two new files plus one DI line). The coordinator merges Contractors first, and Costs Task 1 adds the query on top |
| 3 | The default forecast rule (budget fully spent) may not match the client's QS practice | It's an explicit, documented formula with a per-line override. Easy to change in one function |
