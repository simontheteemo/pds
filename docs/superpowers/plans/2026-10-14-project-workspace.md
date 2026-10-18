# Project Workspace and Programme Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give every project a programme (milestones with planned/forecast/actual dates, derived status and slippage) and turn the project page into a tabbed workspace with an extensible Overview, plus a portfolio summary strip and next-milestone column on the projects list.

**Architecture:** Milestones are new items in the existing `portfolio` DynamoDB table owned by `PDS.Portfolio` (`pk=PROJECT#id, sk=MILESTONE#mid`, also on `gsi1` with `gsi1pk=MILESTONE`). Status is derived on read. The web restructures `ProjectDetailPage` into `?tab=`-addressable Mantine tabs driven by a tab registry and an Overview panel registry; existing module section components are reused unchanged.

**Tech Stack:** .NET 10 Minimal APIs, FluentValidation, AWS SDK DynamoDB; React 19, Mantine 9, TanStack Query 5, react-router 8, zod 4, openapi-typescript.

**Spec:** `docs/superpowers/specs/2026-10-14-project-workspace-design.md`

## Global Constraints

- Foundation module rules: only `Contracts/` and `PortfolioModule` are public in `PDS.Portfolio`; `pk`/`sk` string keys; ProblemDetails errors; camelCase JSON, string enums.
- **No new tests** (user directive). Each task verifies with: `dotnet format PDS.slnx` then `dotnet build PDS.slnx` (0 warnings, existing tests compile); in `web/`: `npm run gen:api && npm run typecheck && npm run lint && npm run build`; in `infra/`: `npx aws-cdk@2.1143.0 synth -c deployment=example-dev` (backend tasks only).
- Run the API with `dotnet run --project src/PDS.Api --urls http://localhost:5093`; Vite with `npm run dev -- --port 5203` (point its `/api` proxy at 5093 via env if supported, else run the API check with curl only). Reuse DynamoDB Local on :8000 if running; never stop it.
- `dotnet build` (Debug) regenerates `web/src/shared/api/openapi.json`; then `npm run gen:api` regenerates `schema.d.ts`. Commit both.
- Policies: reads CanRead; create/update CanWrite; every DELETE CanAdminister. Milestone writes on an archived project → 400 on `projectId`. Unknown project or milestone → 404.
- "Today" = `LocalDate.Today(clock, locale.Value)` (deployment time zone), never `DateTime.Today`.
- Do **not** modify anything under `web/src/features/costs`, `web/src/features/contractors`, or other features' components; only import them. Shared wiring files (`routes.tsx`, `ProjectDetailPage.tsx`, nav) get minimal edits.
- No new packages (web or .NET).
- Commits: conventional (`feat(portfolio): …`), ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- UI copy: sentence case, NZ English ("programme"), no client names.

## Review Focus

1. **Status derivation edge cases (§3.2):** actual set but forecast later → Complete; forecast set without planned → OnTrack/Overdue by forecast, `slippageDays` null; planned passed, forecast in future → AtRisk with positive slippage; forecast earlier than planned → OnTrack with negative slippage; due == today → not Overdue.
2. **Standard set idempotency and config:** pressing "Add standard milestones" twice adds nothing the second time; a project that already has "practical completion" (different case) doesn't get a duplicate; a bad configured entry (blank name, unknown category) is skipped, never a 500.
3. **GSI isolation:** milestone items must never appear in `ListAllAsync` (projects list/export) and project items must never appear in the all-milestones query — the two use different `gsi1pk` values. A milestone item must not break `ProjectItemMapper.FromItem` anywhere (no code path reads the `PROJECT#id` partition without filtering `sk`).
4. **Tabs and navigation:** `?tab=costs` deep link opens Costs; Back returns to the previous tab; unknown tab → Overview; the Edit page and `/projects/:id/costs` still work; phone width (375px) has no horizontal page scroll (tab list scrolls).
5. **Archived / read-only users:** on an archived project or for a Viewer, the Programme tab shows no add/edit/delete/mark-achieved controls, and the API rejects writes (400 `projectId` / 403).

---

### Task 1: Milestone domain, storage and CRUD API

**Files:**
- Create in `src/Modules/PDS.Portfolio/`:
  - `Contracts/MilestoneId.cs`
  - `Domain/MilestoneCategory.cs`, `Domain/MilestoneStatus.cs`, `Domain/MilestoneFields.cs`, `Domain/MilestoneRules.cs`, `Domain/Milestone.cs`
  - `Data/MilestoneItemMapper.cs`, `Data/MilestoneStore.cs`
  - `Features/MilestoneDtos.cs`, `Features/MilestoneValidators.cs`, `Features/MilestoneProblems.cs`, `Features/ListMilestones.cs`, `Features/CreateMilestone.cs`, `Features/UpdateMilestone.cs`, `Features/DeleteMilestone.cs`, `Features/AddStandardMilestones.cs`
  - `PortfolioOptions.cs`
- Modify: `PortfolioModule.cs` (register `MilestoneStore`, options, map endpoints)
- Generated: `web/src/shared/api/openapi.json`, `web/src/shared/api/schema.d.ts`

**Interfaces:**
- Produces (public): `MilestoneId` (same shape as `ProjectId`: `readonly record struct MilestoneId(Guid Value)` with `New()` = `Guid.CreateVersion7()` and `ToString()`).
- Produces (internal, used by Task 2):
  - `MilestoneStore.ListForProjectAsync(ProjectId, ct) → IReadOnlyList<Milestone>`, `ListAllAsync(ct) → IReadOnlyList<Milestone>` (gsi1 query on `gsi1pk = MILESTONE`, paginated), `GetAsync(ProjectId, MilestoneId, ct)`, `CreateAsync(Milestone, ct)`, `UpdateAsync(Milestone, long expectedVersion, ct) → bool`, `DeleteAsync(ProjectId, MilestoneId, long expectedVersion, ct) → bool`.
  - `MilestoneRules.Evaluate(MilestoneFields f, DateOnly today) → MilestoneEvaluation(MilestoneStatus Status, int? SlippageDays, int? DaysOverdue)`, `MilestoneRules.DueDate(MilestoneFields) → DateOnly?` (= forecast ?? planned), `MilestoneRules.ProjectOrder` (an `IComparer<Milestone>`: SortOrder, then DueDate with nulls last, then Name ordinal-ignore-case), `MilestoneRules.NameKey(string)`.
  - `MilestoneDetails.From(Milestone, DateOnly today)`.

- [ ] **Step 1: Domain.**

```csharp
// Domain/MilestoneCategory.cs
internal enum MilestoneCategory { Land, Design, ResourceConsent, BuildingConsent, Construction, Completion, Titles, Sales, Finance, Other }

// Domain/MilestoneStatus.cs
internal enum MilestoneStatus { NotStarted, OnTrack, AtRisk, Overdue, Complete }

// Domain/MilestoneFields.cs
internal sealed record MilestoneFields(
    string Name, MilestoneCategory Category, DateOnly? PlannedDate, DateOnly? ForecastDate, DateOnly? ActualDate,
    string? Owner, string? Notes, int SortOrder);

// Domain/MilestoneRules.cs (core)
internal readonly record struct MilestoneEvaluation(MilestoneStatus Status, int? SlippageDays, int? DaysOverdue);

internal static class MilestoneRules
{
    public const int NameMaxLength = 100;
    public const int OwnerMaxLength = 200;
    public const int NotesMaxLength = 2000;
    public const int MaxSortOrder = 9999;

    public static string NameKey(string name) => name.Trim().ToUpperInvariant();
    public static DateOnly? DueDate(MilestoneFields f) => f.ForecastDate ?? f.PlannedDate;

    public static MilestoneEvaluation Evaluate(MilestoneFields f, DateOnly today)
    {
        var effective = f.ActualDate ?? DueDate(f);
        int? slip = f.PlannedDate is { } planned && effective is { } e ? e.DayNumber - planned.DayNumber : null;
        if (f.ActualDate is not null)
            return new(MilestoneStatus.Complete, slip, null);
        if (DueDate(f) is not { } due)
            return new(MilestoneStatus.NotStarted, null, null);
        if (due < today)
            return new(MilestoneStatus.Overdue, slip, today.DayNumber - due.DayNumber);
        if (f.PlannedDate is { } p && f.ForecastDate is { } fc && fc > p)
            return new(MilestoneStatus.AtRisk, slip, null);
        return new(MilestoneStatus.OnTrack, slip, null);
    }

    public static bool IsSlipping(MilestoneStatus s) => s is MilestoneStatus.AtRisk or MilestoneStatus.Overdue;
    // + Normalise (trim name, Clean owner/notes), EnsureValid (name non-empty ≤100, sort 0..9999, enum defined), ProjectOrder comparer
}
```

`Milestone` aggregate mirrors `Costs.Domain.BudgetLine`: private ctor, `Id`, `ProjectId`, `Fields`, `Created`, `Updated`, `Version`; `Create(ProjectId, MilestoneFields, AuditStamp)` normalises + validates, version 1; `Rehydrate(...)`; `Update(fields, stamp)` increments version. Reuse `Portfolio.Domain.AuditStamp`.

- [ ] **Step 2: Data.** `MilestoneItemMapper`:
  - `Sk(MilestoneId) = "MILESTONE#" + id`; key `pk = "PROJECT#" + projectId.Value` (same partition as the project item), `sk`.
  - `gsi1pk = "MILESTONE"`, `gsi1sk = $"{projectId}#{milestoneId}"`.
  - attributes: `id`, `projectId`, `name`, `category` (enum name), `plannedDate`/`forecastDate`/`actualDate` via `Attr.SetIfPresent`, `owner`, `notes` (omit when empty), `sortOrder` (`Attr.N((long)…)`), audit fields, `version` — same style as `BudgetLineItemMapper`.
  - `MilestoneStore(IAmazonDynamoDB, TableNames)`: table `names.For(PortfolioTable.LogicalName)`. `ListForProjectAsync` = Query `pk = :pk AND begins_with(sk, :prefix)` with `:prefix = "MILESTONE#"`, paginated, ConsistentRead. `ListAllAsync` = Query on `PortfolioTable.Gsi1` with `gsi1pk = :type` (`MILESTONE`), paginated. Create = Put with `attribute_not_exists(pk)`; Update/Delete = versioned conditional Put/Delete returning false on `ConditionalCheckFailedException` (copy `CostStore.PutVersionedAsync`/`DeleteVersionedAsync`).
  - Add a one-line comment on `ProjectItemMapper.ProjectType`/class doc noting the partition also holds `MILESTONE#` items (documentation only).

- [ ] **Step 3: Options.**

```csharp
// PortfolioOptions.cs
internal sealed class StandardMilestoneOption { public string? Name { get; set; } public string? Category { get; set; } }

internal sealed class PortfolioOptions
{
    public const string Section = "Portfolio";
    public static readonly IReadOnlyList<StandardMilestoneOption> DefaultStandardMilestones = [
        new() { Name = "Land settlement", Category = "Land" },
        new() { Name = "Resource consent lodged", Category = "ResourceConsent" },
        new() { Name = "Resource consent granted", Category = "ResourceConsent" },
        new() { Name = "Building consent lodged", Category = "BuildingConsent" },
        new() { Name = "Building consent granted", Category = "BuildingConsent" },
        new() { Name = "Construction start", Category = "Construction" },
        new() { Name = "Practical completion", Category = "Completion" },
        new() { Name = "Code compliance certificate (CCC)", Category = "Completion" },
        new() { Name = "Titles issued", Category = "Titles" },
        new() { Name = "Settlements complete", Category = "Sales" },
    ];
    /// <summary>Null unless configured. A pre-filled list would be appended to by the config binder.</summary>
    public List<StandardMilestoneOption>? StandardMilestones { get; set; }
    public IReadOnlyList<StandardMilestoneOption> EffectiveStandardMilestones =>
        StandardMilestones is { Count: > 0 } configured ? configured : DefaultStandardMilestones;
}
```

Register: `services.AddOptions<PortfolioOptions>().BindConfiguration(PortfolioOptions.Section);`, `services.AddSingleton<MilestoneStore>();`.

- [ ] **Step 4: DTOs and validators.** `IMilestoneInput { string? Name; MilestoneCategory? Category; DateOnly? PlannedDate; DateOnly? ForecastDate; DateOnly? ActualDate; string? Owner; string? Notes; int? SortOrder; }`. `CreateMilestoneRequest(...)` (SortOrder optional: null → after the last), `UpdateMilestoneRequest(..., int? SortOrder, long Version)` (SortOrder required on update). Validators: Name required ≤100; Category NotNull + IsInEnum ("Choose a category."); Owner ≤200; Notes ≤2000; SortOrder 0–9999 when present; update: SortOrder NotNull, Version > 0. Response:

```csharp
internal sealed record MilestoneDetails(
    Guid Id, Guid ProjectId, string Name, MilestoneCategory Category,
    DateOnly? PlannedDate, DateOnly? ForecastDate, DateOnly? ActualDate,
    MilestoneStatus Status, int? SlippageDays, int? DaysOverdue,
    string? Owner, string? Notes, int SortOrder,
    DateTimeOffset CreatedAt, string CreatedBy, DateTimeOffset UpdatedAt, string UpdatedBy, long Version);

internal sealed record MilestoneList(DateOnly Today, List<MilestoneDetails> Items);
```

`MilestoneProblems`: `ProjectArchived()` → field `projectId` "Milestones can't be changed on an archived project."; `DuplicateName()` → field `name` "A milestone with this name already exists."; `InvalidVersion()` → field `version`; `VersionConflict()` → Conflict("This milestone was changed by someone else.", "Reload the programme to see the latest changes, then apply your edits again.").

- [ ] **Step 5: Endpoints** (add to the existing `/api/portfolio` group in `MapPortfolioEndpoints`, one `Map` call each). All look up the project via `ProjectStore.GetAsync` (404 when missing; writes 400 when `IsArchived`).
  - `GET /projects/{id:guid}/milestones` → `Ok<MilestoneList>`, ordered by `MilestoneRules.ProjectOrder`. Name `ListMilestones`.
  - `POST /projects/{id:guid}/milestones` (CanWrite, `WithValidation<CreateMilestoneRequest>`) → 201; duplicate `NameKey` → 400 `name`; default SortOrder = `existing.Count == 0 ? 10 : min((max/10+1)*10, 9999)`. Name `CreateMilestone`.
  - `POST /projects/{id:guid}/milestones/standard` (CanWrite) → `Ok<List<MilestoneDetails>>` of added items. Build all first (skip blank/over-long names, unknown categories via `Enum.TryParse(ignoreCase: true)` + `Enum.IsDefined`, already-present names), then write. Same sort-order continuation as Costs `AddStandardLines`. Name `AddStandardMilestones`.
  - `PUT /projects/{id:guid}/milestones/{milestoneId:guid}` (CanWrite, validation) → 200; 404 unknown; duplicate name among *other* milestones → 400; stale → 409. Name `UpdateMilestone`.
  - `DELETE /projects/{id:guid}/milestones/{milestoneId:guid}?version=` (CanAdminister) → 204; missing version → 400 `version`; stale → 409. Name `DeleteMilestone`.
  - `today` from `LocalDate.Today(clock, locale.Value)`.

- [ ] **Step 6: Verify.** Format, build (0 warnings), regenerate `openapi.json` + `gen:api`, web typecheck, `cdk synth`. Manual (API on :5093): create a project, `POST …/milestones/standard` twice (10 then 0), `PUT` one with planned past date → `Overdue`, set forecast future → `AtRisk` with slippage, set actual → `Complete`; duplicate name → 400; stale version → 409; delete; archive project then POST → 400 `projectId`; `GET /api/portfolio/projects` still lists only projects.

- [ ] **Step 7: Commit** `feat(portfolio): milestones with derived status and standard NZ set`.

---

### Task 2: Contracts queries, portfolio summary, next milestone on the list, milestones export

**Files:**
- Create: `src/Modules/PDS.Portfolio/Contracts/MilestoneSummary.cs`, `Features/GetPortfolioSummary.cs`, `Features/MilestonesExport.cs`, `Features/NextMilestone.cs`
- Modify: `Contracts/IPortfolioQueries.cs`, `Data/PortfolioQueries.cs`, `Features/ListProjects.cs`, `PortfolioModule.cs`
- Generated: `openapi.json`, `schema.d.ts`

**Interfaces:**
- Consumes: Task 1 `MilestoneStore`, `MilestoneRules`, `Milestone`.
- Produces (public):

```csharp
public sealed record MilestoneSummary(
    MilestoneId Id, ProjectId ProjectId, string Name, string Category,
    DateOnly? PlannedDate, DateOnly? ForecastDate, DateOnly? ActualDate,
    string Status, int? SlippageDays, string? Owner, int SortOrder);

// IPortfolioQueries additions
/// <summary>The project's milestones in programme order; empty when none or the project doesn't exist.</summary>
Task<IReadOnlyList<MilestoneSummary>> GetMilestones(ProjectId id, CancellationToken ct);
/// <summary>Every incomplete milestone on non-archived projects, ordered by due date (forecast, else planned; undated last).</summary>
Task<IReadOnlyList<MilestoneSummary>> GetOpenMilestones(CancellationToken ct);
```

- Produces (HTTP):
  - `ProjectListItem` gains a trailing `NextMilestoneDto? NextMilestone` where `internal sealed record NextMilestoneDto(Guid Id, string Name, DateOnly? Date, MilestoneStatus Status)`.
  - `GET /api/portfolio/summary` → `PortfolioSummary(int ActiveProjects, List<StageCount> ByStage, decimal TotalBudget, string Currency, int ProjectsSlipping, int OverdueMilestones)`, `StageCount(ProjectStage Stage, int Count)`. Name `GetPortfolioSummary`.

- [ ] **Step 1:** `PortfolioQueries` takes `MilestoneStore`, `TimeProvider`, `IOptions<LocaleOptions>` too (it's scoped; all are registered). `GetMilestones` = `ListForProjectAsync` → order → map with `Evaluate(today)`. `GetOpenMilestones` = `MilestoneStore.ListAllAsync` filtered to `ActualDate is null`, joined to non-archived projects via `ProjectStore.ListAllAsync` (one query each), ordered by due date (nulls last), then project, then sort order.
- [ ] **Step 2:** `NextMilestone.For(IEnumerable<Milestone> projectMilestones, DateOnly today) → NextMilestoneDto?` = incomplete ones ordered by due date (nulls last) then `ProjectOrder`; first or null.
- [ ] **Step 3:** `ListProjects.Handle` additionally injects `MilestoneStore`, `TimeProvider`, `IOptions<LocaleOptions>`; loads `ListAllAsync` milestones once, groups by `ProjectId` into a dictionary, and `Apply` takes the lookup + today to fill `NextMilestone` only for the page's items. Keep `Apply`'s existing parameters working for existing tests: add an overload or optional parameters (`IReadOnlyDictionary<ProjectId, List<Milestone>>? milestones = null, DateOnly today = default`) so `PDS.Tests` still compiles.
- [ ] **Step 4:** `GetPortfolioSummary`: non-archived projects; active = stage ≠ Completed && status ≠ Cancelled; `ByStage` over all non-archived, in enum order, only stages with count > 0; `TotalBudget` = sum of active budgets (null = 0), `Currency` = `locale.Value.Currency`; `ProjectsSlipping` = active projects with ≥ 1 milestone where `IsSlipping(Evaluate(...).Status)`; `OverdueMilestones` = count of Overdue milestones on active projects. Map in `MapPortfolioEndpoints` (`/summary` on the existing group).
- [ ] **Step 5:** `MilestonesExport : IExportDataset` — `Name = "milestones"`, `Title = "Milestones"`, `Description = "Every project milestone with planned, forecast and actual dates, status and slippage."`; columns `project_code, project_name, milestone_id, name, category, planned_date, forecast_date, actual_date, status, slippage_days, days_overdue, owner, notes, sort_order, updated_at`; rows ordered by project code then programme order; computed status uses today. Register `services.AddScoped<IExportDataset, MilestonesExport>();`. Look at `ProjectsExport`/`ExportTable.From` for the shape.
- [ ] **Step 6: Verify.** Build/format, regenerate API types, typecheck (web may need `ProjectListItem` consumers to compile — `nextMilestone` is additive). Manual: `GET /api/portfolio/summary`, `GET /api/portfolio/projects` shows `nextMilestone`; Reports page lists `milestones` dataset (`GET /api/reporting/…` per README); cashflow GET for a project still returns 200.
- [ ] **Step 7: Commit** `feat(portfolio): milestone contracts queries, portfolio summary and next milestone`.

---

### Task 3: Tabbed project workspace shell with registries

**Files:**
- Create in `web/src/features/portfolio/workspace/`: `projectTabs.tsx`, `ProjectWorkspaceTabs.tsx`, `overviewPanels.tsx`, `OverviewTab.tsx`, `KeyFactsPanel.tsx`
- Create: `web/src/features/portfolio/programme/ProgrammeTab.tsx` (placeholder content in this task: the project dates reference card only; Task 4 fills in the milestones)
- Modify: `web/src/features/portfolio/ProjectDetailPage.tsx`

**Interfaces:**
- Produces:

```ts
// projectTabs.tsx
export type ProjectTabProps = { project: ProjectDetails };
export type ProjectTab = {
  key: string;            // URL value of ?tab=
  label: string;
  Panel: ComponentType<ProjectTabProps>;
  /** Optional: returns a count shown as a red badge on the tab label (0 hides it). */
  useAttentionCount?: (projectId: string) => number;
};
export const projectTabs: ProjectTab[]; // overview, programme, costs, contracts, consents, sales, documents, risks
export const DEFAULT_TAB = 'overview';

// overviewPanels.tsx
export type OverviewPanelProps = { project: ProjectDetails };
export type OverviewPanel = { id: string; span: 'full' | 'half'; Panel: ComponentType<OverviewPanelProps> };
export const overviewPanels: OverviewPanel[]; // this task: [{ id: 'key-facts', span: 'full', Panel: KeyFactsPanel }]

// ProjectWorkspaceTabs.tsx
export function ProjectWorkspaceTabs({ project }: { project: ProjectDetails }): JSX.Element;
export function useProjectTabLink(): (tab: string) => string; // returns `?tab=<key>` href for in-page links
```

- [ ] **Step 1:** `projectTabs` entries. Costs tab Panel renders `<Stack><ProjectCostsCard projectId /><ProjectCashflowCard projectId /></Stack>`; Contracts → `ProjectContractsSection`; Consents → `ProjectConsentsSection`; Sales → `ProjectSalesSection`; Documents → `ProjectDocumentsSection`; Risks → `ProjectRisksSection` — each with `projectId={project.id} projectArchived={project.isArchived}` exactly as today. Overview → `OverviewTab`; Programme → `ProgrammeTab`. A top-of-file doc comment explains how to add a tab (one entry).
- [ ] **Step 2:** `ProjectWorkspaceTabs`: reads `tab` via `useSearchParams`; unknown/missing → `DEFAULT_TAB`. `onChange` calls `setSearchParams` with `tab` (omit the param for overview), **push** (not replace) so Back works, preserving other params. Mantine `<Tabs value keepMounted={false}>`; `<Tabs.List>` wrapped so it scrolls horizontally on narrow screens (`style={{ flexWrap: 'nowrap', overflowX: 'auto' }}` on the list, tabs `style={{ flexShrink: 0 }}`). Each `Tabs.Tab` shows a small red `Badge` (`size="xs" circle`) via a child component that calls `tab.useAttentionCount?.(project.id)` (call hooks in a component per tab so hook order is stable). Scroll to top is not needed.
- [ ] **Step 3:** `KeyFactsPanel`: one Card with `SimpleGrid cols={{ base: 1, sm: 2, lg: 4 }}` of fields: Address (address line, suburb, city), Project manager, Budget, Planned start → completion, Actual start → completion, Land area, Legal description / title reference; description below with `lineClamp={4}`. Reuse the existing `Field` helper — move it from `ProjectDetailPage.tsx` to `workspace/Field.tsx` and export it.
- [ ] **Step 4:** `OverviewTab`: `<SimpleGrid cols={{ base: 1, md: 2 }}>` rendering `overviewPanels`; a `span: 'full'` panel is wrapped in `<div style={{ gridColumn: '1 / -1' }}>`. Doc comment at top of `overviewPanels.tsx` with the "how to add a panel" recipe from spec §4.3.
- [ ] **Step 5:** `ProjectDetailPage`: keep loading/404/error and the header (`ProjectActions` stays exported); replace the body stack (Site/Timeline cards, description, sections) with `<ProjectWorkspaceTabs project={p} />`; keep the created/updated footer line. Remove now-unused imports.
- [ ] **Step 6:** `ProgrammeTab` (this task): a Card "Project dates" with planned/actual start and completion (same table as the old Timeline card) and, for writers on non-archived projects, an "Edit project dates" link to `/projects/:id/edit`; text "Cashflow spreads costs over these dates. Milestones below are the detailed programme."
- [ ] **Step 7: Verify.** gen:api, typecheck, lint, build. Manual in browser if available: every tab renders, `?tab=risks` deep link, Back.
- [ ] **Step 8: Commit** `feat(portfolio): tabbed project workspace with tab and overview panel registries`.

---

### Task 4: Programme tab (milestones UI)

**Files:**
- Create in `web/src/features/portfolio/programme/`: `types.ts`, `api.ts`, `MilestoneStatusBadge.tsx`, `milestoneFormModel.ts`, `MilestoneFormModal.tsx`, `MilestonesTable.tsx`
- Modify: `programme/ProgrammeTab.tsx`, `workspace/projectTabs.tsx` (Programme `useAttentionCount`)

**Interfaces:**
- Consumes: Task 1 HTTP API via generated schema; `handleMutationError` (`shared/mutationError`), `zodValidate` (`shared/validation`), `usePermissions`, `useConfig`, `formatDate`.
- Produces:

```ts
// types.ts
export type MilestoneList = DeepRequired<…ListMilestones 200…>;
export type MilestoneDetails = MilestoneList['items'][number];
export type MilestoneInput / MilestoneUpdateInput  // from POST/PUT request bodies
export const milestoneCategories = ['Land','Design','ResourceConsent','BuildingConsent','Construction','Completion','Titles','Sales','Finance','Other'] as const;
export type MilestoneCategory; export const milestoneCategoryLabel: Record<MilestoneCategory, string>; // 'Resource consent', 'Building consent', …
export type MilestoneStatus = 'NotStarted' | 'OnTrack' | 'AtRisk' | 'Overdue' | 'Complete';
export const milestoneStatusLabel: Record<MilestoneStatus, string>; // 'Not started','On track','At risk','Overdue','Complete'
export const milestoneStatusColor: Record<MilestoneStatus, string>; // gray, green, yellow, red, teal
export const isSlipping = (s: MilestoneStatus) => s === 'AtRisk' || s === 'Overdue';
export const dueDate = (m: MilestoneDetails) => m.forecastDate ?? m.plannedDate;

// api.ts — key ['milestones', projectId]; every mutation invalidates it and ['projects'] and ['portfolio-summary']
export function useMilestones(projectId: string);       // GET list
export function useCreateMilestone(projectId: string);
export function useUpdateMilestone(projectId: string);  // ({ milestoneId, body })
export function useDeleteMilestone(projectId: string);  // ({ milestoneId, version })
export function useAddStandardMilestones(projectId: string);
export function useNextMilestones(projectId: string, count = 5); // derived: incomplete, by dueDate (nulls last), first `count`
export function useSlippingMilestoneCount(projectId: string): number;
```

- [ ] **Step 1:** types + api + badge (`MilestoneStatusBadge({ status, slippageDays, daysOverdue })`: the status label, plus " · 12 d late" for AtRisk with positive slippage and " · 5 d overdue" for Overdue; NotStarted shows "Not started" in gray).
- [ ] **Step 2:** form model + modal, following `costs/BudgetLineFormModal` + `budgetLineFormModel` exactly (keyed inner component, `handleMutationError` with `entity: 'milestone'`). Fields: Name (required), Category (Select, required), Planned / Forecast / Actual (`TextInput type="date"`, in a 3-col `SimpleGrid cols={{ base: 1, sm: 3 }}`; forecast description "Leave blank if still on plan"), Owner, Sort order (`NumberInput`, default after last), Notes. zod mirrors the API limits.
- [ ] **Step 3:** `MilestonesTable` (inside `Table.ScrollContainer minWidth={820}`): columns Milestone (name bold + category dimmed under it, owner if any), Planned, Forecast, Actual, Status (badge), Actions. Slipping rows: none highlighted beyond badge. Actions for writers: **Mark achieved** (only when no actual; one click → PUT with `actualDate = today` from the list response's `today`, success toast "Marked achieved"), Edit; for admins: Delete (confirm Modal, version from row). Errors: 409 → yellow toast and refetch; other → red toast.
- [ ] **Step 4:** `ProgrammeTab`: header row with summary badges (e.g. "3 slipping", "2 complete of 10") and buttons (writers, non-archived): "Add milestone", "Add standard milestones" (toast "Added N milestones" / "All standard milestones already exist"); project dates card from Task 3 kept below or above (above, compact); empty state: "No milestones yet. Add the standard NZ development milestones to get started." with the standard button.
- [ ] **Step 5:** Programme tab `useAttentionCount = useSlippingMilestoneCount`.
- [ ] **Step 6: Verify** web checks; manual in browser if available: add standard, edit dates, mark achieved, delete, statuses update.
- [ ] **Step 7: Commit** `feat(portfolio): programme tab with milestones`.

---

### Task 5: Overview panels — Needs attention, Next milestones, Costs, Sales position

**Files:**
- Create in `web/src/features/portfolio/workspace/`: `attention.ts`, `NeedsAttentionPanel.tsx`, `NextMilestonesPanel.tsx`, `CostsPanel.tsx`, `SalesPositionPanel.tsx`
- Modify: `workspace/overviewPanels.tsx` (add entries), `workspace/projectTabs.tsx` (Consents/Risks `useAttentionCount`)

**Interfaces:**
- Consumes: Task 4 `useMilestones`, `useNextMilestones`, `isSlipping`, `MilestoneStatusBadge`; `useConsents` (`features/consents/api`), `useRisks(projectId, false)` (`features/risks/api`), `useProjectSales` (`features/sales/api`), `SalesSummaryStrip`, `ProjectCostsCard`, `RiskBandBadge`, `DueStateBadge`.
- Produces:

```ts
// attention.ts
export type AttentionItem = {
  key: string;
  tab: string;                 // projectTabs key to link to
  severity: 'red' | 'orange' | 'yellow';
  badge: string;               // e.g. 'Overdue', 'At risk', 'Extreme', 'Condition overdue'
  title: string;
  detail?: string;             // e.g. 'Due 3 Oct 2026 · 15 days overdue'
};
export function useProjectAttention(projectId: string): { items: AttentionItem[]; isLoading: boolean; errors: string[] };
export function useConsentAttentionCount(projectId: string): number;
export function useRiskAttentionCount(projectId: string): number;
```

- [ ] **Step 1: attention sources** (each a small pure function from query data → items, then combined and sorted red → orange → yellow):
  - Milestones: Overdue → red "Overdue" (`detail`: "Due {date} · {n} days overdue"); AtRisk → yellow "At risk" ("Forecast {date} · {n} days late"). Tab `programme`.
  - Consents: for each active consent (`isActive`), each condition with `dueState === 'Overdue'` → red "Condition overdue", title = condition reference + description (truncate 80), detail = consent title + due date; consent `expiryState === 'Overdue'` → red "Consent expired". Tab `consents`.
  - Risks (open = status !== 'Closed'): band Extreme → red, High → orange; badge = band; title = risk title; detail = owner/due date when present. Tab `risks`.
  - Errors from any query go into `errors` (panel shows a small warning line, not a crash).
- [ ] **Step 2:** `NeedsAttentionPanel` (half): Card title "Needs attention" with a count badge; list (max 8, then "and N more" text); each row: severity `Badge`, title, dimmed detail, and the row is a link to `?tab=<tab>` (use `useProjectTabLink`). Empty: green check text "Nothing needs attention."
- [ ] **Step 3:** `NextMilestonesPanel` (half): Card "Next milestones", up to 5 rows: name, date in use (formatted; "Not scheduled" when null), status badge; footer link "Open programme". Empty: text + "Add standard milestones" button (writers, non-archived).
- [ ] **Step 4:** `CostsPanel` = `<ProjectCostsCard projectId={project.id} />` (half). `SalesPositionPanel` (half): Card "Sales position": `SalesSummaryStrip` when `units.length > 0`, else dimmed "No units recorded." + link to `?tab=sales`.
- [ ] **Step 5:** `overviewPanels` order: key-facts (full), needs-attention (half), next-milestones (half), costs (half), sales (half). Consents tab `useAttentionCount = useConsentAttentionCount`, Risks tab `= useRiskAttentionCount`.
- [ ] **Step 6: Verify** web checks; manual: overdue condition and an Extreme risk appear and link to their tab.
- [ ] **Step 7: Commit** `feat(portfolio): overview panels with needs attention, next milestones, costs and sales`.

---

### Task 6: Projects list summary strip and next milestone column; docs

**Files:**
- Create: `web/src/features/portfolio/PortfolioSummaryStrip.tsx`
- Modify: `web/src/features/portfolio/ProjectsPage.tsx`, `web/src/features/portfolio/api.ts` (add `usePortfolioSummary`, key `['portfolio-summary']`; `useStoreProject` also invalidates it), `web/src/features/portfolio/types.ts` (`PortfolioSummary` type), `README.md` (one line: `Portfolio:StandardMilestones` config, under per-module settings; Portfolio entry mentions milestones)

**Interfaces:**
- Consumes: Task 2 `GET /api/portfolio/summary`, `ProjectListItem.nextMilestone`; Task 4 `MilestoneStatusBadge`, `isSlipping`.

- [ ] **Step 1:** `PortfolioSummaryStrip`: Card/Paper with a `Group` of figures — "Active projects" (number), "Total budget" (money), "Slipping" ("N projects · M milestones overdue", red when > 0), and stage badges (`Badge variant="light"` "Design 2", …). Wraps on phones (`SimpleGrid cols={{ base: 2, sm: 4 }}` for figures, stage badges in a wrapping `Group` below). Loading: `Skeleton height={64}`; error: hide the strip quietly (list still usable).
- [ ] **Step 2:** `ProjectsPage`: render the strip between the title row and the filters. Add a "Next milestone" column after Status: name, dimmed date below, and `MilestoneStatusBadge` only when slipping; "—" when null. Bump `Table.ScrollContainer minWidth` to 880.
- [ ] **Step 3:** README lines.
- [ ] **Step 4: Verify** all web checks, `dotnet build`, `cdk synth`. Manual: strip numbers match the data; next milestone column.
- [ ] **Step 5: Commit** `feat(portfolio): portfolio summary strip and next milestone column`.

---

## Spec coverage

| Spec | Task |
|---|---|
| §3.1–3.4 milestones, status, standard set, API | 1 |
| §2.1 Contracts queries; §5 summary + next milestone; export | 2 |
| §4.1 tabs, URL, reuse; §4.3 registries; key facts | 3 |
| Programme tab, mark achieved, project dates reconciliation row | 3, 4 |
| §4.2 overview panels, Needs attention, tab badges | 5 |
| §5 list UI; README config | 6 |
