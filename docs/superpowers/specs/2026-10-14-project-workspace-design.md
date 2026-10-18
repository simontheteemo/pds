# PDS Project Workspace and Programme (v1) — Design Spec

- Date: 2026-10-18
- Author: @simon (with Claude)
- Status: **Proceeding on the user's "autonomous, verify later" instruction.** Every decision below was made by Claude and is open to review.
- Builds on: [Foundation spec](2026-09-29-foundation-design.md) · [Readiness review](../../readiness/2026-10-18-readiness-review.md) (P0 item 2) · [Costs spec](2026-10-14-costs-design.md) (standard-lines pattern)

## 1. Purpose

Turn the project page from a long stack of record registers into a **workspace** a project manager can run the project from, and give every project a **programme**: the key dates, whether they are slipping, and what needs attention now.

### 1.1 The weekly workflow this serves

| Role | What they do each week | What PDS gives them after this change |
|---|---|---|
| Project manager | Update the programme after the site/consultant meeting: re-forecast dates, record what was achieved | Programme tab: one table of milestones; re-forecast in an edit modal; **Mark achieved** sets the actual date to today in one click |
| Project manager | Open the project and see what's wrong | Overview tab: next milestones, "Needs attention" (slipping/overdue milestones, overdue consent conditions and expired consents, High/Extreme open risks) with links into the relevant tab |
| Development manager | Scan the portfolio: how many projects at which stage, total budget, which projects are slipping | Projects list: summary strip (active projects by stage, total budget, projects with slipping milestones) and a **Next milestone** column with status |
| Development manager | Set up a new project | **Add standard milestones** seeds the NZ development set in one click; planned dates are filled in afterwards |
| Accountant | Check cost position for a project | Costs tab (unchanged components) and the Costs card on the Overview |
| Admin | Configure the standard milestone set for the client | `Portfolio:StandardMilestones` in configuration |

### 1.2 Stated vs decided

| Stated (coordinator brief) | Decided by Claude (review later) |
|---|---|
| Milestones are project-owned; put them in `PDS.Portfolio` unless there's a strong reason for a new module | **In `PDS.Portfolio`**, as their own items in the existing `portfolio` table (`pk=PROJECT#id, sk=MILESTONE#mid`). No new table, no CDK change. The existing `gsi1` index also carries milestones (`gsi1pk=MILESTONE`) so one query reads all milestones for the portfolio views |
| Name, type/category, planned/forecast/actual dates, derived status, slippage, owner, notes, sort order | As stated. Category is a fixed enum (§3.1). Owner is free text (≤ 200). Notes ≤ 2000. Sort order 0–9999 |
| Status: Not started, On track, At risk (forecast later than planned), Overdue (planned or forecast passed, no actual), Complete | Derived on read, in the deployment time zone (§3.2). "Planned or forecast passed" is read as **the date in use** (forecast if set, else planned) being before today. A milestone whose planned date has passed but which has been re-forecast to a future date is **At risk**, not Overdue: the PM has re-planned it and the slip is shown in days. **Not started** = no planned and no forecast date (not yet scheduled) |
| Slippage in days | `slippageDays = (actual ?? forecast ?? planned) − planned` when planned is set, else null. Positive = late. Overdue items also get `daysOverdue = today − (forecast ?? planned)` |
| "Add standard milestones" from configuration, like `Costs:StandardLines` | `Portfolio:StandardMilestones` = list of `{ name, category }`; the code default is the 10-item NZ set (§3.3). Adds only names not already on the project (case-insensitive), with sort orders continuing after the highest existing one. Idempotent |
| Public Contracts query for the Home dashboard | **Additive:** `IPortfolioQueries.GetMilestones(ProjectId)` and `IPortfolioQueries.GetOpenMilestones()` (every incomplete milestone on non-archived projects, with status), plus `MilestoneId` and `MilestoneSummary` records (§2.1) |
| Decide whether the project's start/completion come from milestones | **They stay separate fields.** `GetSchedule` (Cashflow) is unchanged. Rationale: the project dates are the contract-level dates the cashflow curve spreads costs over; milestone categories don't map one-to-one onto "start" and "completion" for every project type, and auto-syncing would silently move cashflow when a PM re-forecasts. Reconciliation is visual: the Programme tab shows the project's planned/actual start and completion above the milestone table, with an Edit link. Cost if wrong: one follow-up to derive dates from flagged milestones |
| Tabs: Overview · Programme · Costs · Contracts · Consents · Sales · Documents · Risks, URL-addressable | `?tab=<key>` query string (`overview` default). Not `/projects/:id/:tab`: `/projects/:id/costs` and `/projects/:id/cashflow` are already full pages, so a path segment would collide. Switching tabs pushes a history entry, so Back returns to the previous tab |
| Reuse section components | Each tab renders the existing section component(s) unchanged. Costs tab = `ProjectCostsCard` + `ProjectCashflowCard` (no edits to `features/costs` or `features/contractors`) |
| Overview: key facts, next 3–5 milestones, Costs card, sales position, Needs attention | As stated. Next milestones = the first 5 incomplete milestones ordered by date in use. Sales position = `SalesSummaryStrip` from the existing project-sales query, shown only when units exist |
| Overview easy to extend | An exported **panel registry** (`overviewPanels` array of `{ id, span, Panel }`) and a **tab registry** (`projectTabs`). Adding a panel is one array entry (§4.3) |
| Portfolio summary strip; next milestone per project if cheap | **Cheap, so both done.** `GET /api/portfolio/summary` and a `nextMilestone` field on the project list item. Both reuse the existing list-all query plus one milestones query on `gsi1` (no per-project fan-out) |
| — | Milestone writes on an archived project → 400 on `projectId`; reads still work. Create/update need CanWrite; delete needs CanAdminister (same policy rule as every other module) |
| — | Milestones get a `milestones` export dataset on the Reports page (cheap; one class) |
| No new tests (standing directive) | Verify by format, build, typecheck, lint, synth, manual API checks and a browser check |

### 1.3 Success criteria

- The project page has the eight tabs; each is reachable by URL and Back works.
- A PM can add the standard set, fill planned dates, re-forecast one, mark one achieved, and see statuses and slippage update.
- The Overview shows next milestones, the cost position, the sales position and a Needs attention list that links to the right tab.
- The projects list shows the summary strip and each project's next milestone.
- Cashflow still works unchanged.

## 2. Architecture

- `PDS.Portfolio` gains a `Milestone` aggregate (Domain), `MilestoneItemMapper` and store methods (Data), and milestone endpoints (Features), all internal. Public additions are in `Contracts/` only.
- No new module, table, package or host wiring. `PortfolioModule` gets the new endpoint `Map` calls, an options binding and an export registration.
- Web: `features/portfolio` gains the workspace (`ProjectDetailPage` restructure, `workspace/` folder for tabs and panels) and `programme/` folder (milestones UI). Other features are only imported.

### 2.1 Public contract additions

```csharp
public readonly record struct MilestoneId(Guid Value) { New(); ToString(); }

public sealed record MilestoneSummary(
    MilestoneId Id, ProjectId ProjectId, string Name, string Category,
    DateOnly? PlannedDate, DateOnly? ForecastDate, DateOnly? ActualDate,
    string Status, int? SlippageDays, string? Owner, int SortOrder);

// IPortfolioQueries (additive)
Task<IReadOnlyList<MilestoneSummary>> GetMilestones(ProjectId id, CancellationToken ct);       // project order
Task<IReadOnlyList<MilestoneSummary>> GetOpenMilestones(CancellationToken ct);                 // incomplete, non-archived projects, by date in use
```

`Status` values are the `MilestoneStatus` names: `NotStarted`, `OnTrack`, `AtRisk`, `Overdue`, `Complete`.

## 3. Milestones

### 3.1 Fields

| Field | Rule |
|---|---|
| Name | required, trimmed, ≤ 100, unique per project (case-insensitive) |
| Category | enum `MilestoneCategory`: `Land`, `Design`, `ResourceConsent`, `BuildingConsent`, `Construction`, `Completion`, `Titles`, `Sales`, `Finance`, `Other` |
| PlannedDate, ForecastDate, ActualDate | optional dates (no ordering constraints — a forecast may be earlier than planned) |
| Owner | optional, ≤ 200 (free text for now; becomes a user link with the people directory) |
| Notes | optional, ≤ 2000 |
| SortOrder | 0–9999; create defaults to after the last one when omitted |
| Version | optimistic concurrency, as elsewhere |

### 3.2 Status (derived, `today` = `LocalDate.Today` in `Locale:TimeZone`)

1. `ActualDate` set → **Complete**.
2. `due = ForecastDate ?? PlannedDate`; null → **NotStarted**.
3. `due < today` → **Overdue**.
4. `PlannedDate` set and `ForecastDate > PlannedDate` → **AtRisk**.
5. Otherwise → **OnTrack**.

`SlippageDays` and `DaysOverdue` as in §1.2. Slipping = `AtRisk` or `Overdue`.

### 3.3 Default standard set

| Name | Category |
|---|---|
| Land settlement | Land |
| Resource consent lodged | ResourceConsent |
| Resource consent granted | ResourceConsent |
| Building consent lodged | BuildingConsent |
| Building consent granted | BuildingConsent |
| Construction start | Construction |
| Practical completion | Completion |
| Code compliance certificate (CCC) | Completion |
| Titles issued | Titles |
| Settlements complete | Sales |

Configured entries with a blank or over-long name or an unknown category are skipped; nothing partial is written on bad config (build all first, then write).

### 3.4 API (`/api/portfolio`, CanRead group)

| Method | Route | Policy | Notes |
|---|---|---|---|
| GET | `/projects/{id}/milestones` | CanRead | `{ today, items: MilestoneDetails[] }` in project order (sortOrder, then date in use, then name) |
| POST | `/projects/{id}/milestones` | CanWrite | 201 `MilestoneDetails`; duplicate name → 400 `name` |
| POST | `/projects/{id}/milestones/standard` | CanWrite | 200 `MilestoneDetails[]` (only the added ones) |
| PUT | `/projects/{id}/milestones/{mid}` | CanWrite | stale version → 409 |
| DELETE | `/projects/{id}/milestones/{mid}?version=` | CanAdminister | 204 |
| GET | `/summary` | CanRead | portfolio summary (§5) |

`MilestoneDetails` = fields + `status`, `slippageDays`, `daysOverdue`, audit stamps, `version`. Unknown project → 404; unknown milestone → 404; writes on archived → 400 `projectId`.

## 4. Project workspace (web)

### 4.1 Layout

Header (code · name, status/stage/archived badges, Edit/Archive) stays above the tabs. Tabs scroll horizontally on phones. Tab panels mount only when active, so each tab loads its own data on first visit.

| Tab | Content |
|---|---|
| Overview | panel registry (§4.3) |
| Programme | project dates reference row + milestones table + actions |
| Costs | `ProjectCostsCard`, `ProjectCashflowCard` |
| Contracts | `ProjectContractsSection` |
| Consents | `ProjectConsentsSection` |
| Sales | `ProjectSalesSection` |
| Documents | `ProjectDocumentsSection` |
| Risks | `ProjectRisksSection` |

The Programme, Consents and Risks tab labels show a small red count badge when that area has attention items (slipping milestones, overdue conditions/expired consents, High/Extreme open risks) — computed by the same hook the Needs attention panel uses, so it costs no extra requests once the Overview has loaded. Unknown `?tab=` falls back to Overview.

### 4.2 Overview panels (default order)

1. **Key facts** (full width): site address, stage/status, PM, budget, planned/actual start and completion, description (clamped).
2. **Needs attention** (half): list of items, each with a badge, a one-line description, and a link to the tab. Empty state: "Nothing needs attention."
3. **Next milestones** (half): up to 5 incomplete milestones by date in use, with status badge and date; "Open programme" link; empty state offers "Add standard milestones" for writers.
4. **Costs** (half): `ProjectCostsCard` as is.
5. **Sales position** (half): `SalesSummaryStrip` when the project has units; otherwise "No units recorded" with a link to the Sales tab.

### 4.3 Extension point

`web/src/features/portfolio/workspace/overviewPanels.tsx`:

```ts
export type OverviewPanelProps = { project: ProjectDetails };
export type OverviewPanel = { id: string; span: 'full' | 'half'; Panel: ComponentType<OverviewPanelProps> };
export const overviewPanels: OverviewPanel[] = [ ... ];
```

To add a panel (e.g. "Open actions" from `feat/actions-home`): write a component taking `OverviewPanelProps`, then add one entry to `overviewPanels`. Likewise `projectTabs` in `workspace/projectTabs.tsx` takes `{ key, label, Panel, useAttentionCount? }` for a new tab. The attention list is built by `useProjectAttention(projectId)` in `workspace/attention.ts`, which returns `AttentionItem[]`; another feature can contribute items by adding a source there.

## 5. Projects list

- **Summary strip** above the filters, from `GET /api/portfolio/summary`:
  - `activeProjects`: non-archived, stage ≠ Completed, status ≠ Cancelled.
  - `byStage`: count of non-archived projects per stage (zero stages omitted in the UI).
  - `totalBudget` (+ `currency`): sum of active projects' budgets.
  - `projectsSlipping`: active projects with at least one AtRisk/Overdue milestone; `overdueMilestones`: count of Overdue milestones on active projects.
- **Next milestone column**: `nextMilestone { name, date, status }` on `ProjectListItem` — the first incomplete milestone by date in use (undated ones last); null when none. The column shows the name, date and a status badge when slipping.
- Cost: the list endpoint already reads every project; it adds one `gsi1` query for all milestones. ADR-0002's "fine up to a few thousand items" scale applies.

## 6. Error handling

Same as the other modules: ProblemDetails, field errors for validation, 409 on stale versions, toasts on the web via the shared mutation-error helper.

## 7. Verification

No new tests (standing directive). `dotnet format` + `dotnet build` (0 warnings), web gen:api/typecheck/lint/build, `cdk synth`, manual API checks against DynamoDB Local (create project, add standard set twice, edit/forecast/complete, delete, stale version, archived write, summary and list), and a browser check at desktop and phone width.
