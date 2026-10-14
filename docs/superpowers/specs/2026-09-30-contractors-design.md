# PDS Contractors & Contracts (v1) — Design Spec

- Date: 2026-09-30
- Author: @simon (with Claude)
- Status: **Proceeding on the user's "keep going, verify later" instruction.** Every decision below was made by Claude and is open to review.
- Builds on: [Foundation spec](2026-09-29-foundation-design.md) · [Risk Registry spec](2026-09-30-risk-registry-design.md) · architecture set A-04 (Contract → Contractor, Contract → Project)

## 1. Purpose

Replace the contractor knowledge that lives in people's heads and inboxes with a central register:
- who we work with
- whether their insurance and licences are current
- which contracts they hold on which projects

This also gives an evidence trail for disputes or performance issues. Cost Tracking, the next module, will point its cost lines at these contracts.

### Stated vs decided

| Stated by @simon (planning note) | Decided by Claude (review later) |
|---|---|
| Central register of contractors, subcontractors, consultants | One organisation-wide register. `Type`: Contractor, Subcontractor, Consultant, Supplier |
| Contract terms | Contracts belong to a project and a contractor. Fields: title, reference, form, value, dates, retention %, status |
| Insurance/licence expiry | Three optional expiry dates: public liability insurance, professional indemnity insurance, licence (with a licence number). The compliance status is calculated |
| Performance history | v1 shows each contractor's contracts across all projects. Ratings and performance notes are deferred |
| — | Contract forms: NZS 3910, NZS 3916, NZS 3917, NZIA SCC, Consultancy (CCCS), Other |
| — | Contract status: Draft → Active → PracticalCompletion → Complete, or Terminated (any direction is allowed; it's a record, not a workflow) |
| — | Contractors are deactivated, never deleted. Inactive contractors can't receive new contracts |
| — | Compliance: **Expired** if any recorded date has passed; **ExpiringSoon** if any falls within 30 days; **NotRecorded** if none are recorded; otherwise **Current**. "Today" is the deployment time zone's date |
| — | Managers and Admins create and edit contractors and contracts. Viewers read. Only Admins deactivate or reactivate contractors |
| No new tests (standing directive) | Verify by build, type-check, lint, synth and manual API checks |

### Success criteria
- **Contractors page (new nav item):** lists contractors with type, trade, compliance badge and status, with search, a type filter and a "show inactive" switch.
- **Contractor pages:**
  - **Create and edit:** separate pages.
  - **Detail page:** shows the contact details, compliance dates with badges, and the contractor's contracts across all projects.
- **Contracts section on the project detail page:** lists the project's contracts, with add and edit in a modal. You pick an active contractor from a searchable list.
- **Ownership:** one new module, `PDS.Contractors`, owns one table, `contractors`. It uses only `PDS.Portfolio.Contracts`.

## 2. Architecture

- A new module, `src/Modules/PDS.Contractors`, with the same shape as Portfolio and Risks:
  - `Contracts/` is public and holds `ContractorId` and `ContractId`.
  - Everything else is internal.
  - `ContractorsModule` handles registration.
- It references `PDS.Portfolio` for Contracts only (`ProjectId`, `IPortfolioQueries.GetSummaries`).
- The host adds one line each: `AddContractorsModule()` and `MapContractorsEndpoints()`. Infra adds `.. ContractorsModule.Tables`.
- The web app adds a `web/src/features/contractors` feature, a "Contractors" nav link, and routes `/contractors`, `/contractors/new`, `/contractors/:id` and `/contractors/:id/edit`. The project detail page renders `ProjectContractsSection`.

## 3. Data

**Contractor** (organisation-wide):

| Field | Rules |
|---|---|
| `Id` | `ContractorId`, UUIDv7 |
| `Name` | Required, ≤ 200 |
| `Type` | Contractor, Subcontractor, Consultant or Supplier |
| `Trade` | ≤ 100, e.g. "Civil", "Electrical", "Structural engineer" |
| `Nzbn` | Optional, 13 digits |
| `ContactName` | ≤ 200 |
| `Email` | ≤ 200, validated as an email |
| `Phone` | ≤ 50 |
| `Address` | ≤ 300 |
| `PublicLiabilityExpiry` | DateOnly? |
| `ProfessionalIndemnityExpiry` | DateOnly? |
| `LicenceNumber` | ≤ 50 |
| `LicenceExpiry` | DateOnly? |
| `Notes` | ≤ 4000 |
| `IsActive` | |
| Other | Audit fields and `Version` |

**Contract** (belongs to a project and a contractor):

| Field | Rules |
|---|---|
| `Id` | `ContractId` |
| `ProjectId`, `ContractorId` | |
| `Title` | Required, ≤ 200 |
| `Reference` | ≤ 50 |
| `Form` | NZS3910, NZS3916, NZS3917, NZIASCC, Consultancy, Other |
| `Value` | Money? in the deployment currency; ≥ 0, 2 decimal places |
| `StartDate`, `EndDate` | DateOnly?; end ≥ start |
| `RetentionPercent` | decimal? 0–20, 1 decimal place |
| `Status` | Draft, Active, PracticalCompletion, Complete or Terminated |
| `Notes` | ≤ 4000 |
| Other | Audit fields and `Version` |

**Table `contractors`**: `pk`/`sk` plus GSI `gsi1` (`gsi1pk`/`gsi1sk`).

| Item | pk | sk | gsi1pk | gsi1sk |
|---|---|---|---|---|
| Contractor | `CONTRACTOR#<id>` | `PROFILE` | `CONTRACTOR` | `<lower-case name>#<id>` |
| Contract | `PROJECT#<projectId>` | `CONTRACT#<id>` | `CONTRACTOR#<contractorId>` | `CONTRACT#<id>` |

| Access pattern | Operation |
|---|---|
| List contractors | Query gsi1 where `gsi1pk = CONTRACTOR` (all pages), then filter and search in memory. Ordered by name |
| Get a contractor | GetItem |
| A project's contracts | Query where `pk = PROJECT#<id>` and `begins_with(sk, "CONTRACT#")` |
| A contractor's contracts | Query gsi1 where `gsi1pk = CONTRACTOR#<id>` |
| Writes | PutItem. Create uses `attribute_not_exists(pk)`; update uses `#version = :expected` (409 on mismatch) |

Contractor names aren't unique: two firms can share a name. Contract list items include the contractor name, fetched with one BatchGetItem per list.

## 4. API

Contractors (the group requires CanRead):

| Method | Route | Policy | Behaviour |
|---|---|---|---|
| GET | `/api/contractors?search=&type=&includeInactive=false` | CanRead | `ContractorListItem[]` with calculated `compliance`, ordered by name |
| GET | `/api/contractors/{id}` | CanRead | `ContractorDetails` including `compliance` and the per-date status |
| POST | `/api/contractors` | CanWrite | `201 ContractorDetails` |
| PUT | `/api/contractors/{id}` | CanWrite | Body includes `version`; 409 on a stale version |
| POST | `/api/contractors/{id}/deactivate` and `/activate` | CanAdminister | Body `{ version }`. No-op → 200 unchanged |
| GET | `/api/contractors/{id}/contracts` | CanRead | `ContractListItem[]` across projects, including project code and name |

Contracts (the group requires CanRead):

| Method | Route | Policy | Behaviour |
|---|---|---|---|
| GET | `/api/contractors/projects/{projectId}/contracts` | CanRead | 404 for an unknown project. Returns `ContractListItem[]` ordered by status (Active first), then start date |
| GET | `.../{contractId}` | CanRead | `ContractDetails` or 404 |
| POST | `/api/contractors/projects/{projectId}/contracts` | CanWrite | 404 unknown project; 400 on `projectId` if archived; 400 on `contractorId` if the contractor is unknown or inactive |
| PUT | `.../{contractId}` | CanWrite | Body includes `version`. The contractor can't change (400 on `contractorId` if it differs). 409 on a stale version |

## 5. UI

- **Contractors list:**
  - Controls: search box, type filter, "Show inactive" switch (all roles) and a "New contractor" button (canWrite).
  - Columns: Name (link), Type, Trade, Compliance badge (Current green, ExpiringSoon yellow, Expired red, NotRecorded gray), Status (Active, or an Inactive badge).
- **Contractor detail:**
  - Header: name with type, status and compliance badges.
  - Actions: Edit (canWrite); Deactivate or Activate (canAdminister).
  - Cards: Contact; Compliance (each date with its own badge and the licence number); Notes.
  - Contracts table: project code/name link, title, form, value, dates, status.
- **Contractor create/edit:** a form page like the project form. A 409 on edit shows the same conflict alert as project edit.
- **Project detail, Contracts section:**
  - A table: contractor (link), title, reference, form, value, start–end, status.
  - "Add contract" (canWrite and project not archived) and Edit per row (canWrite).
  - The modal form has a contractor Select (searchable, active contractors only, from `/api/contractors`), which is disabled when editing.
  - The contractor's compliance badge appears next to the Select once one is chosen. It's a warning only and doesn't block.

## 6. Out of scope (v1)
- Performance ratings and notes
- Documents (insurance certificates)
- Expiry reminders or emails
- Variations, claims and payments (Cost Tracking)
- Contact persons beyond one
- Deleting contractors or contracts
- Linking risks to contractors
