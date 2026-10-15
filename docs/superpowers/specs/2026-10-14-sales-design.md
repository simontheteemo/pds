# PDS Sales Pipeline (v1) — Design Spec

- Date: 2026-10-14
- Author: @simon (with Claude)
- Status: **Proceeding autonomously under the parallel-build brief.** Every decision below was made by Claude and is open to review.
- Builds on: [Foundation spec](2026-09-29-foundation-design.md) · [Risk Registry spec](2026-09-30-risk-registry-design.md) · [Contractors spec](2026-09-30-contractors-design.md)

## 1. Purpose

Replace the sales spreadsheet kept for each development with a register of the project's saleable units or lots and where each one is in the sale process. The team can see at a glance:
- how many units are still available, under offer, conditional, unconditional or settled
- the value listed versus the value contracted under signed sale and purchase (S&P) agreements
- the revenue already settled

### Stated vs decided

| Stated (task brief) | Decided by Claude (review later) |
|---|---|
| Per-project register of units/lots: identifier, type, bedrooms, areas, list price | One entity, **Unit**, covering apartments, townhouses, houses and bare-land lots. Fields: identifier (unique per project, case-insensitive), type, bedrooms, bathrooms, car parks, floor area m², land area m², list price, notes |
| — | Unit types: Apartment, Townhouse, House, Lot (bare-land section), Commercial, Other |
| Sale status pipeline: Available → Reserved/Under offer → Conditional → Unconditional → Settled, plus Withdrawn | `SaleStatus`: Available, Reserved, Conditional, Unconditional, Settled, Withdrawn. **Any move is allowed** (a record, not a workflow), because sales fall over and go back to Available |
| Buyer details and key sale fields: agreement date, sale price, deposit, unconditional date, settlement date | Buyer: name, email, phone, solicitor. Sale: agreement date, sale price, deposit, unconditional date, settlement date. One current sale per unit, embedded in the unit item |
| — | Required fields grow with the status: Reserved needs the buyer name; Conditional also needs the agreement date and sale price; Unconditional also needs the unconditional date; Settled also needs the settlement date. Before Settled, the settlement date is the due date; at Settled it's the actual date |
| — | Moving a unit to **Available or Withdrawn clears the buyer and sale fields**. There's no sale history in v1, so a fallen-over sale is not kept |
| — | Date order: unconditional ≥ agreement; settlement ≥ agreement and ≥ unconditional. Deposit ≤ sale price. Dates may be in the future (no "today" checks) |
| Per-project sales summary: counts by status, total list value vs contracted value, settled revenue | Counts per status and total. **List value** = sum of list prices of units not Withdrawn. **Contracted value** = sum of sale prices of Conditional, Unconditional and Settled units. **Settled value** = sum of sale prices of Settled units. Calculated on read, not stored |
| Cross-project dashboard optional | **Built.** A "Sales" nav page lists every project that has units, with its summary and a totals row. It's cheap: one GSI query plus `IPortfolioQueries.GetSummaries` |
| Money uses `PDS.Shared.Money` | List price, sale price and deposit are `Money?` in the deployment currency (`Locale:Currency`), ≥ 0 with at most 2 decimal places. DTOs carry `…Amount` plus one `currency` |
| Only depend on `PDS.Portfolio.Contracts` | Module `PDS.Sales` references `PDS.Portfolio` for `ProjectId`, `ProjectSummary` and `IPortfolioQueries` only |
| NZ terminology, generic, no client names | UI labels: "S&P agreement date", "Unconditional date", "Settlement date", "Buyer's solicitor". No client names |
| — | Managers and Admins create and edit units and update sales. Viewers read. **Nothing is deleted**: a unit entered by mistake is edited or Withdrawn |
| — | Archived projects keep their units visible and their sales editable, but accept no new units |
| — | Identifier uniqueness is checked in the handler against the project's units (one query). There's no guard item, so two simultaneous creates could race; acceptable at the expected user count |
| — | Units are ordered by identifier with numeric-aware comparison ("Lot 2" before "Lot 10") |
| No new tests (standing directive) | Verify by format, build, type-check, lint, synth and manual API checks against DynamoDB Local |

### Success criteria
- On a project's detail page, a **Sales** card shows the summary (counts by status, list value, contracted value, settled value) and the unit register.
- A Manager can add a unit, edit it, and update its sale: move it through Reserved → Conditional → Unconditional → Settled, entering buyer and sale details as required at each step, or back to Available or Withdrawn. A Viewer sees the same card with no actions.
- A **Sales** page in the nav lists every project with units, its counts and values, and portfolio totals.
- Units live in a `sales` DynamoDB table owned by the new `PDS.Sales` module, which only uses `PDS.Portfolio.Contracts`.
- CDK creates the table and grants access through the existing module-tables loop.

## 2. Architecture

- A new module, `src/Modules/PDS.Sales`, with the same shape as Risks:
  - `Contracts/` is public and holds only `UnitId`.
  - `Domain/`, `Data/` and `Features/` are internal.
  - `SalesModule.cs` is public: `Tables`, `AddSalesModule()` and `MapSalesEndpoints()`.
- The host adds one line each for registration and mapping. Infra appends `.. SalesModule.Tables`.
- The web app adds `web/src/features/sales`, a "Sales" nav link, the route `/sales`, and one `<ProjectSalesSection>` line on the project detail page.

## 3. Data

**Unit**:

| Field | Rules |
|---|---|
| `Id` | `UnitId`, UUIDv7 |
| `ProjectId` | |
| `Identifier` | Required, trimmed, ≤ 50. Unique within the project, ignoring case |
| `Type` | Apartment, Townhouse, House, Lot, Commercial, Other |
| `Bedrooms`, `Bathrooms`, `CarParks` | int?, 0–20 |
| `FloorAreaSqm`, `LandAreaSqm` | decimal?, 0–1,000,000, ≤ 2 decimal places |
| `ListPrice` | Money?, ≥ 0, ≤ 2 decimal places |
| `Notes` | ≤ 4000 |
| `Status` | SaleStatus; new units start Available |
| `Sale` | `SaleDetails?`; null when Available or Withdrawn |
| Other | `Created`, `Updated` (AuditStamp), `Version` |

**SaleDetails**: `BuyerName` (≤ 200), `BuyerEmail` (≤ 200, email), `BuyerPhone` (≤ 50), `BuyerSolicitor` (≤ 200), `AgreementDate`, `SalePrice` (Money?), `Deposit` (Money?), `UnconditionalDate`, `SettlementDate`.

Required by status:

| Status | Also requires |
|---|---|
| Available, Withdrawn | Nothing; sale details are cleared |
| Reserved | `BuyerName` |
| Conditional | + `AgreementDate`, `SalePrice` |
| Unconditional | + `UnconditionalDate` |
| Settled | + `SettlementDate` |

**Table `sales`**: `pk`/`sk` plus GSI `gsi1` (`gsi1pk`/`gsi1sk`).

| Item | pk | sk | gsi1pk | gsi1sk |
|---|---|---|---|---|
| Unit | `PROJECT#<projectId>` | `UNIT#<unitId>` | `UNIT` | `PROJECT#<projectId>#UNIT#<unitId>` |

Amounts are stored as `listPriceAmount`, `salePriceAmount` and `depositAmount` (N) with one `currency` (S) attribute shared by all of them; the domain requires every amount on a unit to use the same currency.

| Access pattern | Operation |
|---|---|
| A project's units | Query `pk = PROJECT#<id> AND begins_with(sk, "UNIT#")` (all pages) |
| Get one | GetItem |
| All units (dashboard) | Query gsi1 `gsi1pk = UNIT` (all pages), grouped by project in memory |
| Create | PutItem `attribute_not_exists(pk)` |
| Update unit / sale | PutItem `#version = :expected` → 409 on mismatch |

## 4. API

Project units, group `/api/sales/projects/{projectId}/units` (tag "Sales", CanRead):

| Method | Route | Policy | Behaviour |
|---|---|---|---|
| GET | `` | CanRead | 404 unknown project. `ProjectSales { summary, units: UnitDetails[] }`, units ordered by identifier (numeric-aware) |
| GET | `/{unitId}` | CanRead | `UnitDetails` or 404 |
| POST | `` | CanWrite | Body = unit fields. 404 unknown project; 400 on `projectId` if archived; 400 on `identifier` if taken. `201 UnitDetails` with status Available |
| PUT | `/{unitId}` | CanWrite | Body = unit fields + `version`. 400 on `identifier` if another unit has it. 409 on a stale version |
| PUT | `/{unitId}/sale` | CanWrite | Body = `status`, buyer and sale fields, `version`. Per-status required fields and date order → 400 on the field. Available/Withdrawn clears the sale. 409 on a stale version |

Portfolio-wide (tag "Sales", CanRead):

| Method | Route | Behaviour |
|---|---|---|
| GET | `/api/sales/summary` | `ProjectSalesSummary[]`: `projectId`, `projectCode`, `projectName`, `projectArchived`, `summary`, for each project with at least one unit, ordered by project code. Units whose project is unknown to Portfolio are skipped |

`SalesSummary`: `totalUnits`, `available`, `reserved`, `conditional`, `unconditional`, `settled`, `withdrawn`, `listValue`, `contractedValue`, `settledValue`, `currency`.

Errors use the existing ProblemDetails conventions (400 ValidationProblemDetails with camelCase keys, 404, 409).

## 5. UI

- **Project detail → Sales card** (below Risks):
  - Header "Sales" with "Add unit" (canWrite and project not archived).
  - **Summary strip:** a badge per status with its count, then List value, Contracted value, Settled value, and "Sold" = (Unconditional + Settled) / (total − Withdrawn).
  - **Table:** Unit, Type, Bed/Bath/Car (e.g. "3 · 2 · 1"), Floor m², Land m², List price, Status badge, Buyer, Sale price, Key date, Actions.
    - Key date: Reserved → none; Conditional → "S&P " + agreement date; Unconditional → "Settles " + settlement date when set, otherwise "Unconditional " + unconditional date; Settled → "Settled " + settlement date.
    - Actions (canWrite): "Edit" (unit modal) and "Sale" (sale modal).
  - Empty state: "No units recorded."
- **Unit modal:** Identifier, Type, Bedrooms, Bathrooms, Car parks, Floor area, Land area, List price, Notes.
- **Sale modal:** Status select (all six, "Reserved / under offer" label). For any status other than Available or Withdrawn it shows Buyer name, email, phone, solicitor, S&P agreement date, Sale price, Deposit, Unconditional date, Settlement date, with required asterisks following the status table. Choosing Available or Withdrawn on a unit that has sale details shows "Saving clears the buyer and sale details."
- Server 400s map onto fields; unmapped keys go to a top alert; a 409 closes the modal, refreshes the list and shows a notification (the Risks pattern).
- **Sales page** (`/sales`, nav "Sales"): a table of projects (code · name link, Archived badge), Units, Available, Under offer, Conditional, Unconditional, Settled, List value, Contracted, Settled value, % sold; then a totals row. Empty state: "No units recorded on any project yet."
- Status colours: Available blue, Reserved yellow, Conditional orange, Unconditional teal, Settled green, Withdrawn gray.

## 6. Out of scope (v1)
- Sale history (fallen-over sales, previous buyers), price change history
- Agents and commission, marketing campaigns
- Deposit held in trust tracking, GST treatment, settlement statements
- Condition types and due dates (finance, due diligence, title), reminders
- Documents (S&P agreement PDFs)
- Bulk import of a unit schedule from a spreadsheet
- Deleting units
- Linking sales revenue to Cost Tracking or cashflow
