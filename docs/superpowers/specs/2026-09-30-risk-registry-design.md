# PDS Risk Registry (v1) — Design Spec

- Date: 2026-09-30
- Author: @simon (with Claude)
- Status: **Proceeding on the user's "keep going, verify later" instruction.** Every decision below was made by Claude and is open to review.
- Builds on: [Foundation spec](2026-09-29-foundation-design.md) · [ADR-0002 DynamoDB](../../adr/0002-dynamodb.md) · architecture set sheets A-03/A-04

## 1. Purpose

Replace ad-hoc risk tracking with a per-project risk register. For each project, the team logs risks with a likelihood and impact rating, an owner and a mitigation, and keeps them up to date until they're closed. The aim is to move from reacting to crises to tracking risks before they hit.

### Stated vs assumed

| Stated by @simon | Decided by Claude (review later) |
|---|---|
| Risks per project (geotech, consenting delays, disputes, contractor default, market exposure) | Categories: Geotechnical, Consenting, Contractual, ContractorDefault, Market, Financial, Design, HealthAndSafety, Environmental, Other |
| Likelihood/impact, owner, mitigation | 5×5 matrix; score = likelihood × impact; bands Low 1–4, Medium 5–9, High 10–16, Extreme 20–25 |
| v1 = the per-project register only | No portfolio-wide view, no residual rating, no review history (all later candidates) |
| — | Status: Open → Mitigating → Closed; closing needs a note; closed risks can be reopened; closed risks can't be edited until reopened |
| — | Managers (and Admins) create, edit, close and reopen. Viewers read. Nothing is deleted. |
| — | Archived projects keep their risks visible but accept no new ones |
| — | Owner is free text (a person's name), like Project Manager on projects |
| No new tests (standing directive) | Verify by build, type-check, lint, synth and a manual API check |

### Success criteria
- On a project's detail page, a Manager can add a risk, edit it, close it with a note, and reopen it. A Viewer sees the same list with no actions.
- Open risks are listed highest score first. Closed risks are hidden unless "Show closed" is on.
- Risks live in their own `risks` DynamoDB table owned by a new `PDS.Risks` module, which only uses `PDS.Portfolio.Contracts`.
- CDK creates the new table and grants the API function access to it, with no hand edits to the stacks beyond passing the module's table list.

## 2. Architecture

- A new module project, `src/Modules/PDS.Risks`, with the same shape as Portfolio:
  - `Contracts/` is public and holds only `RiskId`.
  - `Domain/`, `Data/` and `Features/` are internal.
  - `RisksModule.cs` is public: `Tables`, `AddRisksModule()` and `MapRisksEndpoints()`.
- The module references `PDS.Portfolio` for its **Contracts only**: `ProjectId` and `IPortfolioQueries.GetSummaries`.
- The host adds one line each: `AddRisksModule()` and `MapRisksEndpoints()`.
- Infra passes `[..PortfolioModule.Tables, ..RisksModule.Tables]`. The existing loop creates `<name>-risks`, grants the API function access, and sets `Tables__risks`.

## 3. Data

**Domain `Risk`**:
- Identity: `Id` (`RiskId`, UUIDv7) and `ProjectId`.
- `Fields`:
  - `Title` (required, ≤ 200)
  - `Category`
  - `Description` (≤ 4000)
  - `Likelihood` and `Impact` (1–5)
  - `Mitigation` (≤ 4000)
  - `Owner` (≤ 200)
  - `DueDate` (DateOnly?)
- `Status` (Open | Mitigating | Closed)
- `ClosingNote` (≤ 2000), `Closed` (AuditStamp?)
- `Created`, `Updated`, `Version`

**Table `risks`**: `pk = PROJECT#<projectId>`, `sk = RISK#<riskId>`. No GSI.

| Access pattern | Operation |
|---|---|
| Risks of a project | `Query pk = PROJECT#<id> AND begins_with(sk, "RISK#")` (all pages) |
| Get one | `GetItem(pk, sk)` |
| Create | `PutItem` with `attribute_not_exists(pk)` |
| Update / close / reopen | `PutItem` with `#version = :expected` → 409 on mismatch |

Score and band are calculated, not stored.

## 4. API

Base path `/api/risks/projects/{projectId}/risks`. The whole group requires CanRead.

| Method | Route | Policy | Behaviour |
|---|---|---|---|
| GET | `?includeClosed=false` | CanRead | 404 if project unknown. Returns `RiskDetails[]` (full risks, so the UI can edit without another fetch): not closed first, then score desc, then title |
| GET | `/{riskId}` | CanRead | `RiskDetails` or 404 |
| POST | `` | CanWrite | 404 unknown project; 400 on `projectId` if the project is archived; `201 RiskDetails` |
| PUT | `/{riskId}` | CanWrite | Body = fields + `status` (Open or Mitigating) + `version`. 400 on `status` if the risk is Closed ("Reopen the risk before editing it."). 409 on a stale version |
| POST | `/{riskId}/close` | CanWrite | Body `{ version, note }`, where the note is required. Already closed → 200 unchanged |
| POST | `/{riskId}/reopen` | CanWrite | Body `{ version }`. Sets Open and clears the closing note and stamp. Already open → 200 unchanged |

`RiskDetails` includes the calculated `score` and `band`. Errors use the same ProblemDetails conventions as Portfolio.

## 5. UI

The project detail page gets a **Risks** card below the existing cards:
- **Header:** "Risks" and an "Add risk" button (canWrite and project not archived), plus a "Show closed" switch.
- **Table columns:** Rating (band badge "High · 12", coloured Low green, Medium yellow, High orange, Extreme red), Title, Category, Owner, Due, Status, Actions.
- **Actions** (canWrite only):
  - Edit and Close, when the risk isn't closed.
  - Reopen, when it's closed.
- **Add/edit:** a modal form with Title, Category, Likelihood (1–5 select with labels), Impact (1–5 select with labels), a live score/band preview, Owner, Due date, Status (edit only: Open or Mitigating), Description and Mitigation. Server 400s map onto the fields. A 409 shows an alert with "Reload latest".
- **Close:** a modal asking for the closing note.
- **Empty state:** "No open risks recorded." (or "No risks recorded." when "Show closed" is on).

Likelihood labels: 1 Rare, 2 Unlikely, 3 Possible, 4 Likely, 5 Almost certain. Impact labels: 1 Insignificant, 2 Minor, 3 Moderate, 4 Major, 5 Severe.

## 6. Out of scope (v1)
- A portfolio-wide risk view or heat map
- Residual rating
- Review history
- Links to consents or contractors
- Reminders on due dates
- Deleting risks
