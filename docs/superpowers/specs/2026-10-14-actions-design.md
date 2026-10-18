# Actions, people directory and Home dashboard: design

- Date: 2026-10-18
- Readiness item: P0 #3 (`docs/readiness/2026-10-18-readiness-review.md`)
- Branch: `feat/actions-home`
- Status: decided autonomously (user unavailable); every call is in the table below, for later review.

## 0. The real workflow (why this exists)

Nobody can answer "what do I need to do today?" in PDS. Walking through a normal week:

| Role | What they do | What this design gives them |
|---|---|---|
| Project manager | After a site or consultant meeting, writes up who owes what by when, then chases it. | **Add action** on the project, in a quick modal. The owner is picked from real users and defaults to me. The due date defaults to one week out. It can optionally be linked to the risk, consent or contract it's about. |
| Project manager | Opens the app on Monday morning. | **Home** is the landing page. **My actions** shows my overdue items first, then those due this week, then later, and each has a one-click **Done**. |
| Development manager | Needs to see what's slipping across all projects. | Home shows **Overdue across the portfolio** (actions plus overdue consent conditions and expiries), **High and extreme open risks**, and **Projects needing attention** (At risk, Delayed or On hold). Every row links to its project. |
| Accountant (often a Viewer) | Gets given "pay the deposit" or "chase the retention release" and needs to tick it off. | The **owner of an action can complete or reopen it even as a Viewer**. Everything else stays Manager or Admin. |
| Admin | Adds staff in the Cognito console. | People come straight from the Cognito groups, so a new user can be assigned work within about 5 minutes, with no extra admin screen. |

What's out of scope: there are no notifications or email digest (P2 #7). Actions are not raised from a meetings log (P1 #5; the `Meeting` link type is reserved for that). There are no external assignees.

## 1. Stated vs decided

| Stated (task brief) | Decided by Claude (review later) |
|---|---|
| People directory, host-level or in `PDS.Shared` | The **interface** `IPeopleDirectory` and record `Person(Id, Name, Email, Role)` live in `PDS.Shared/People`. This is a small, additive shared change, made so that modules (Actions) can resolve owners without referencing the host. The **implementations** live in the host, `PDS.Api/People`, because they need the AWS Cognito SDK and `Auth:DevUser`, and modules shouldn't take a Cognito dependency. `GET /api/people` is a host endpoint (CanRead). |
| Deployed: Cognito `ListUsers` + each user's group | It uses **`ListUsersInGroup` for each of the three role groups** (Admin, Manager, Viewer) instead of `ListUsers` plus a groups call per user. That's 3 paged calls instead of N+1, and it gives exactly the assignable set: a user in no group can't use PDS, so they shouldn't be assignable. A user in several groups takes the highest role. Disabled users are skipped. `Id` is the `sub` attribute, which matches `ICurrentUser.Id`. `Name` is the `name` attribute, falling back to `email` and then the username. **Cost if wrong:** none, because the API returns the same people either way. |
| Cache briefly | The directory is cached in memory for **5 minutes** per Lambda instance (a lock plus timestamp, with no new package). A new user can be assigned within about 5 minutes. |
| Grant read-only Cognito permissions | `userPool.Grant(api, "cognito-idp:ListUsersInGroup")` in `AppStack`, scoped to the one pool ARN, plus the env var `People__UserPoolId`. |
| Local dev: configured list incl. dev user | `People:Users` (an array of `{id,name,email,role}`) in `appsettings.Development.json`, containing the dev user plus two sample colleagues (Manager and Viewer). `Auth:DevUser` is always merged in if it's missing. **Source selection:** Cognito when `People:UserPoolId` is set, otherwise Configured. |
| Small interface | `IPeopleDirectory { ListAsync(ct); FindAsync(id, ct) }`. |
| Store assignee by id + denormalised name | The action stores `ownerId` and `ownerName`. The server resolves the name from the directory on create, and on edit when the owner changes. An unknown `ownerId` returns 400 on `ownerId`. If the owner is **unchanged** but has since left the directory, the edit keeps the stored name and succeeds, so the UI keeps working. |
| Actions module, own table | `PDS.Actions` owns the `actions` table. The item is `pk=PROJECT#<projectId>`, `sk=ACTION#<actionId>`. |
| Fields | `title` (required, ≤ 200), `description` (≤ 4000), `ownerId`/`ownerName` (required), `dueDate` (optional; the UI defaults it to today+7), `priority` (`Low`, `Normal`, `High`; default Normal), `status` (`Open`, `InProgress`, `Done`, `Cancelled`), `completedAt`/`completedBy` (set when the action becomes Done or Cancelled, and cleared on reopen), and audit stamps plus a version. An action without a due date is never overdue. |
| Link `{module, recordId, label}` to risk/consent/contract/milestone/meeting, no existence check | An optional `link` with `module` ∈ `Risk, Consent, Contract, Milestone, Meeting`, a typed reference that isn't checked. It's the same shape, validation and label snapshot as Documents (`DocumentLink`), copied into the module rather than shared, because the module rule is "no cross-module references". The web picker offers the project's risks, consents and contracts (the existing read APIs). `Milestone` and `Meeting` are accepted by the API and rendered, but have no picker source until those records exist, which is a one-line follow-up for milestones. |
| Uses only `PDS.Portfolio.Contracts` | Yes, plus `PDS.Shared`. |
| Per project: list, add, edit, complete, reopen | `GET/POST /api/actions/projects/{projectId}/actions`, `GET/PUT …/{actionId}`, and `POST …/{actionId}/complete`, `…/cancel` and `…/reopen`. `PUT` edits the fields and moves the action between Open and InProgress; a closed action must be reopened first, the same as Risks. A no-op complete or reopen returns 200 even with a stale version (the Risks rule). |
| Cross-project: `/mine`, `/overdue` | `GET /api/actions/mine` lists the current user's **open** (Open or InProgress) actions on non-archived projects, sorted with overdue first, then by due date with no-date last. `GET /api/actions/overdue` lists every open action with `dueDate < today` (NZ local date), oldest first, on non-archived projects. Both return `ActionSummary` with the project code and name. |
| GSI on owner, probably on due | Both are **sparse** GSIs, written only while the action is open: **gsi1** is `gsi1pk=OWNER#<ownerId>`, `gsi1sk=<due or 9999-12-31>#<id>` (mine), and **gsi2** is `gsi2pk=OPEN`, `gsi2sk=<due>#<id>`, only when there's a due date (overdue = `gsi2sk < today`). A closed action drops out of both indexes. The single `OPEN` partition is fine at single-tenant scale (hundreds of open actions); **cost if wrong:** a hot partition beyond about 1,000 writes per second, which won't happen here. |
| Public Contracts query for counts, if the dashboard needs it | **Not added.** The dashboard reads the HTTP endpoints, so no module needs counts in-process yet. The project-workspace Overview can call `/api/actions/projects/{id}/actions` too. `PDS.Actions.Contracts` holds only `ActionId`. |
| Roles; can an owner complete as a Viewer? | Viewers read. Managers and Admins create, edit, cancel, complete and reopen. **The action's owner may complete and reopen their own action whatever their role** (the policy is CanRead, plus an in-handler check that returns 403 otherwise). That's how an accountant or other Viewer ticks off what they owe. A Viewer owner still can't edit fields, cancel, or set In progress. **Cost if wrong:** a Viewer can change the status of their own items only, so the risk is low. |
| Archived projects | No create, edit or cancel (400 on `projectId`, the module convention). Complete and reopen are still allowed, so people can close out. `mine` and `overdue` exclude archived projects. |
| Home is the new landing page | `/` renders `HomePage`. `/projects` stays in the nav, and **Home** is added at the top of the nav. |
| Panels | **My actions:** overdue, then due within 7 days, then later. The first 8 are shown, with a "Show all (n)" toggle and a one-click Done. **Overdue across the portfolio:** overdue actions merged with `/api/consents/due` items whose state is `Overdue`, sorted by date. **High and extreme open risks:** from a new additive `GET /api/risks/high` in `PDS.Risks` (open or mitigating, band High or Extreme, non-archived projects, highest score first). **Projects needing attention:** status counts plus a list of AtRisk, Delayed and OnHold projects, from the existing projects list (the list has no `updatedAt`, so "recently updated" isn't possible without changing Portfolio). |
| Every item links to its project and tab | Every link goes through one helper, `projectTabLink(projectId, tab)` → `/projects/<id>?tab=<tab>`. The project-workspace branch owns the tab mechanism, so the coordinator changes this helper only, if its scheme differs. Today the query string is ignored and the link opens the project page. |
| Slots for slipping milestones and invoices awaiting approval | The Home page is an ordered list of self-contained panel components (`features/home/panels/*`). Each panel fetches its own data and can be added with one import plus one line. A marked comment in `HomePage.tsx` shows exactly where `SlippingMilestonesPanel` (from `IPortfolioQueries.GetMilestones` on `feat/project-workspace`) and `InvoicesAwaitingApprovalPanel` (from `feat/contractor-invoices`) go. No "coming soon" placeholders are shown to users. |
| Project page: `ProjectActionsSection` | `features/actions/ProjectActionsSection` (table, add, edit, complete, cancel and reopen, a "Show closed" toggle, and an overdue badge). The **only** edit to `ProjectDetailPage.tsx` is one line (plus its import) that renders it above Risks. |
| No new tests | Verified with format, build, gen:api, typecheck, lint, web build, synth, manual API checks against DynamoDB Local, and a browser check. |

### Success criteria

- A Manager opens the app and lands on Home. They see their overdue actions at the top, and can tick one Done from Home.
- On a project, a Manager adds an action: the owner is picked from real people and defaults to themselves, the due date defaults to a week out, and it can be linked to a risk. It appears in the owner's My actions.
- A Viewer who owns an action can complete and reopen it. A Viewer who doesn't own it gets 403. A Viewer can't create.
- Overdue actions and overdue consent conditions from every active project appear in one Home panel. High and extreme open risks appear in another.
- Deployed, `GET /api/people` lists the Cognito users in the PDS groups. Locally, it lists the configured users.

## 2. Architecture

```
PDS.Shared/People/IPeopleDirectory.cs   public: Person, IPeopleDirectory
PDS.Api/People/                          host: ConfiguredPeopleDirectory, CognitoPeopleDirectory,
                                         CachedPeopleDirectory, PeopleOptions, AddPeopleDirectory(), GET /api/people
src/Modules/PDS.Actions/                 Contracts/ActionId; Domain; Data (table + 2 GSIs); Features
src/Modules/PDS.Risks/Features/ListHighRisks.cs   additive: GET /api/risks/high
web/src/features/actions/                api, types, form model, ActionFormModal, ProjectActionsSection, PersonSelect
web/src/features/home/                   HomePage, projectTabLink, panels/*
infra                                    ActionsModule.Tables; People__UserPoolId env; ListUsersInGroup grant
```

Shared wiring edits are one line each: `PDS.slnx`, `PDS.Api.csproj` (the project reference plus the `AWSSDK.CognitoIdentityProvider` package), `Program.cs` (`AddPeopleDirectory`, `AddActionsModule`, `MapActionsEndpoints`), `infra/Program.cs`, `PDS.Infra.csproj`, `routes.tsx`, the nav, and `ProjectDetailPage.tsx`. In `AppStack` there's an env var plus a grant (2 lines), and the people endpoint goes in `HostEndpoints`.

**New package:** `AWSSDK.CognitoIdentityProvider` (4.x, the same SDK family already used for DynamoDB, S3 and SNS) is referenced only by `PDS.Api`. It's the AWS-managed way to read the user pool, and there's no alternative without hand-signing requests.

### 2.1 Domain rules (PDS.Actions)

- `IsOpen` = status ∈ {Open, InProgress}.
- `IsOverdue(today)` = open, has a due date, and `dueDate < today`, where today = `LocalDate.Today(clock, Locale)`.
- `Create`: status is Open, version 1.
- `Update(fields, status ∈ {Open, InProgress})`: throws if the action is closed.
- `Complete` → Done with the `completed` stamp, and `Cancel` → Cancelled with the stamp. Both return false (a no-op) if the action is already closed.
- `Reopen` → Open and clears the stamp, returning false if the action is already open.

### 2.2 HTTP surface

| Op | Route | Policy |
|---|---|---|
| ListProjectActions | `GET /api/actions/projects/{projectId}/actions?includeClosed` | CanRead (404 for an unknown project) |
| GetAction | `GET …/{actionId}` | CanRead |
| CreateAction | `POST …` `{title, description?, ownerId, dueDate?, priority?, link?}` | CanWrite |
| UpdateAction | `PUT …/{actionId}` `{…fields, status, version}` | CanWrite |
| CompleteAction | `POST …/{actionId}/complete` `{version}` | CanRead + (writer or owner) |
| ReopenAction | `POST …/{actionId}/reopen` `{version}` | CanRead + (writer or owner) |
| CancelAction | `POST …/{actionId}/cancel` `{version}` | CanWrite |
| ListMyActions | `GET /api/actions/mine` | CanRead |
| ListOverdueActions | `GET /api/actions/overdue` | CanRead |
| ListPeople | `GET /api/people` | CanRead |
| ListHighRisks | `GET /api/risks/high` | CanRead |

The project list sorts open actions first: overdue, then by due date (no date last), then by priority (High first), then by title. Closed actions come after these, newest completed first.

`ActionDetails` contains `id, projectId, title, description, ownerId, ownerName, dueDate, priority, status, isOverdue, link, completedAt, completedBy, created*/updated*, version`. `ActionSummary` contains `ActionDetails` plus `projectCode` and `projectName`, as a flat record.

### 2.3 Errors

- Validation errors return 400 with field errors (FluentValidation, camelCase paths, `link.module` and so on).
- An unknown owner returns 400 on `ownerId`.
- An archived project returns 400 on `projectId`.
- A closed action on PUT returns 400 on `status`.
- A stale version returns 409.
- A non-owner Viewer completing or reopening gets 403.
- A Cognito failure in `/api/people` returns 500 (logged); the cache is unchanged.

## 3. Web

- `PersonSelect` is a searchable Select over `/api/people` (cached 5 minutes in react-query). It shows the name and role, and keeps a "(no longer a user)" option for a stored owner who has since left the directory.
- `ActionFormModal` (add and edit): title, owner (default: me), due date (default: today+7), priority (a SegmentedControl, default Normal), linked record (Risk, Consent or Contract picker) and description. Status (Open or In progress) appears on edit only.
- `ProjectActionsSection`: a card with an id anchor `actions`, a table (Due with an Overdue badge, Title with the link label, Owner, Priority, Status) and row buttons: Done for a writer or owner, Edit and Cancel for a writer, and Reopen on closed actions. It has a "Show closed" toggle.
- `HomePage`: a greeting line and a responsive grid of panels. Each panel handles its own loading, error and empty states, so one failing API never blanks the page.

## 4. Follow-ups (noted in the PR)

1. **Slipping milestones panel:** once `feat/project-workspace` merges, add `SlippingMilestonesPanel` using its milestones API, and add `Milestone` options to the actions link picker.
2. **Invoices awaiting approval panel:** once `feat/contractor-invoices` merges, add `InvoicesAwaitingApprovalPanel`.
3. Move `ProjectActionsSection` into the right workspace tab, and point `projectTabLink` at the real tab scheme.
4. Include actions in the reminder digest (P2 #7).
