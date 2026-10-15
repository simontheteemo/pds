# PDS Consents (v1) — Design Spec

- Date: 2026-10-14
- Author: @simon (with Claude)
- Status: **Proceeding on the user's "autonomous, verify later" instruction.** Every decision below was made by Claude and is open to review.
- Builds on: [Foundation spec](2026-09-29-foundation-design.md) · [Risk Registry spec](2026-09-30-risk-registry-design.md) · [Contractors spec](2026-09-30-contractors-design.md)

## 1. Purpose

Replace the spreadsheet of consents with a per-project consent register. Each project records the consents and statutory approvals it needs (resource, subdivision, building, s224(c), code compliance certificate and others), where each one is in its lifecycle, its key dates, and the conditions attached to it with their due dates.

The register flags anything that needs attention: overdue or soon-due conditions, and granted consents that are close to lapsing or expiring. Today these slip because they live in council letters and people's calendars.

### Stated vs decided

| Stated (task brief / roadmap) | Decided by Claude (review later) |
|---|---|
| Consent register per project | One module, `PDS.Consents`, owns one table, `consents`. Consents belong to a project. Only `PDS.Portfolio.Contracts` is used |
| Types: resource, building, subdivision/224c, CCC, other | `ConsentType`: `ResourceConsent`, `SubdivisionConsent`, `BuildingConsent`, `Section224c`, `CodeComplianceCertificate`, `Other`. It's a fixed enum with display labels in the UI. "Configurable" is met by a free-text **Title** on every consent (e.g. "Land use consent – stage 2", "Engineering plan approval") plus `Other`, rather than a per-deployment list. Per-deployment type lists would add a settings round-trip for little gain; revisit if a client needs one |
| Authority (council) | Free text `Authority` (≤ 200), e.g. "Auckland Council", "Waka Kotahi". No council list in code (generic, no client data) |
| Reference | Free text `Reference` (≤ 100): the council's application or consent number |
| Status lifecycle | `ConsentStatus`: `Preparing`, `Lodged`, `InfoRequested` (s92 / RFI), `Granted`, `Declined`, `Withdrawn`. Any change is allowed: it's a record, not a workflow (same stance as contract status). `Declined` and `Withdrawn` are **inactive** and raise no attention flags |
| Lodged / granted / expiry dates | `LodgedDate`, `GrantedDate` ("granted / issued", which covers CCC and 224c issue dates) and `ExpiryDate` ("lapse / expiry"), all `DateOnly?`. Rules: if both are set, granted ≥ lodged and expiry ≥ granted. `GrantedDate` is required when the status is `Granted` |
| Conditions with due dates and completion | A condition is its own item under the consent. Fields: `Reference` (e.g. "Condition 14", ≤ 50), `Description` (required, ≤ 2000), `DueDate`, completion (`CompletedOn` date + who/when). Complete and reopen are separate actions. Conditions are never deleted (same as risks); a mistake is edited |
| Derived upcoming/overdue indicators | Calculated on the server in the deployment time zone (the same `LocalDate` approach as Contractors). See §3.3 |
| Reminders: EventBridge digest, or defer? | **Deferred to v2.** v1 ships an in-app **"Consents due" page** (new nav item) that lists every overdue and due-soon item across active projects. That is the same list a digest email would contain. See §6 for the reasoning and the v2 sketch |
| — | Managers and Admins create and edit consents and conditions, and complete or reopen conditions. Viewers read. Nothing is deleted |
| — | Archived projects keep their consents visible but accept no new consents or conditions (400 on `projectId`). Their items don't appear on the "Consents due" page |
| No new tests (standing directive) | Verify by format, build, gen:api, typecheck, lint, web build, synth and manual API checks |

### Success criteria
- On a project's detail page, a Manager can:
  - add a consent, edit it, and move it through its statuses
  - add conditions to a consent, edit them, and complete or reopen them
- Each consent shows an attention badge (Overdue / Due soon) and condition counts. Each condition shows its due state. A Viewer sees the same thing with no actions.
- A "Consents" nav item opens a cross-project list of overdue and due-soon conditions and expiring consents, soonest first, each linking to its project.
- A new `consents` DynamoDB table is created by CDK through the existing table loop. The only shared-file edits are one line each in the host, solution, infra, routes, nav and project detail page.

## 2. Architecture

- A new module, `src/Modules/PDS.Consents`, with the same shape as Risks:
  - `Contracts/` is public and holds `ConsentId` and `ConditionId`.
  - `Domain/`, `Data/` and `Features/` are internal.
  - `ConsentsModule.cs` is public: `Tables`, `AddConsentsModule()` and `MapConsentsEndpoints()`.
- It references `PDS.Portfolio` for Contracts only (`ProjectId`, `ProjectSummary`, `IPortfolioQueries`). There's no dependency on Contractors or Risks.
- The host adds one line each: `AddConsentsModule()` and `MapConsentsEndpoints()`. Infra adds `.. ConsentsModule.Tables`.
- The web app adds `web/src/features/consents`:
  - a `ProjectConsentsSection` rendered on the project detail page
  - a `ConsentsDuePage` at `/consents`, with a "Consents" nav link

## 3. Data

### 3.1 Domain

**Consent**

| Field | Rules |
|---|---|
| `Id` | `ConsentId`, UUIDv7 |
| `ProjectId` | |
| `Type` | `ConsentType` |
| `Title` | Required, ≤ 200 |
| `Authority` | ≤ 200 |
| `Reference` | ≤ 100 |
| `Status` | `ConsentStatus` |
| `LodgedDate`, `GrantedDate`, `ExpiryDate` | `DateOnly?`. Granted ≥ lodged; expiry ≥ granted (each only when both are set). Granted date required when the status is `Granted` |
| `Notes` | ≤ 4000 |
| Other | `Created`, `Updated` (`AuditStamp`), `Version` |

**Condition**

| Field | Rules |
|---|---|
| `Id` | `ConditionId`, UUIDv7 |
| `ProjectId`, `ConsentId` | |
| `Reference` | ≤ 50 |
| `Description` | Required, ≤ 2000 |
| `DueDate` | `DateOnly?` |
| `CompletedOn` | `DateOnly?`. Set by Complete (must not be in the future), cleared by Reopen |
| `Completed` | `AuditStamp?`: who recorded the completion and when |
| Other | `Created`, `Updated`, `Version` |

Fields are edited with PUT. Completion is changed only through the complete and reopen actions, and editing a completed condition is allowed. The record stays accurate, and unlike risks there's no status gate to protect.

### 3.2 Table `consents`

Key schema `pk`/`sk`, with **no GSI**.

| Item | pk | sk |
|---|---|---|
| Consent | `PROJECT#<projectId>` | `CONSENT#<consentId>` |
| Condition | `PROJECT#<projectId>` | `CONSENT#<consentId>#COND#<conditionId>` |

Each item carries an `itemType` attribute (`Consent` or `Condition`) so a mixed query result can be split without parsing keys.

| Access pattern | Operation |
|---|---|
| A project's consents with their conditions | `Query pk = PROJECT#<id> AND begins_with(sk, "CONSENT#")` (all pages), then group conditions under their consent |
| One consent with its conditions | `Query pk AND begins_with(sk, "CONSENT#<consentId>")`. The ids are fixed-length GUIDs, so the prefix can't match another consent |
| One condition | `GetItem` |
| Cross-project "due" list | `Scan` (all pages), then filter in memory. The table holds only consent data, so reading it all costs the same as a constant-partition GSI query and needs no index. Fine at the expected volume (hundreds of consents, low thousands of conditions). Revisit with a sparse due-date GSI when the v2 digest arrives |
| Create | `PutItem` with `attribute_not_exists(pk)` |
| Update / complete / reopen | `PutItem` with `#version = :expected` → 409 on mismatch |

### 3.3 Derived due state

`DueState`: `None`, `Upcoming`, `DueSoon`, `Overdue`, `Done`. "Today" is the date in `Locale:TimeZone`.

- **Condition:**
  - `Done` if it's completed.
  - Otherwise `None` if its consent is inactive (Declined or Withdrawn) or it has no due date.
  - Otherwise `Overdue` if due < today.
  - Otherwise `DueSoon` if due ≤ today + **30** days. A condition due today is DueSoon, not Overdue.
  - Otherwise `Upcoming`.
- **Consent expiry** (`ExpiryState`):
  - Applies only when the status is `Granted` and `ExpiryDate` is set; `None` otherwise.
  - `Overdue` (shown as "Expired") if expiry < today.
  - `DueSoon` (shown as "Expiring soon") if expiry ≤ today + **90** days. Lapse dates need more lead time to apply for an extension.
  - Otherwise `Upcoming`.
- **Consent attention:** the worst of `ExpiryState` and its conditions' states, ranked Overdue > DueSoon. It's `None` when neither appears, and always `None` for an inactive consent.
- **Counts on each consent:** `conditionCount`, `openConditionCount`, `overdueConditionCount`, `dueSoonConditionCount`.

The two windows are named constants in `ConsentRules` (`ConditionWarningDays = 30`, `ExpiryWarningDays = 90`).

## 4. API

Project-scoped: `/api/consents/projects/{projectId}/consents`. The group requires CanRead.

| Method | Route | Policy | Behaviour |
|---|---|---|---|
| GET | `` | CanRead | 404 for an unknown project. Returns `ConsentDetails[]`, each with its `conditions` and the derived fields. Order: active before inactive, then type order, then title. Conditions within a consent: open first, then due date (nulls last), then reference |
| GET | `/{consentId}` | CanRead | `ConsentDetails` or 404 |
| POST | `` | CanWrite | 404 unknown project; 400 on `projectId` if archived; `201 ConsentDetails` |
| PUT | `/{consentId}` | CanWrite | Fields + `status` + `version`. 409 on a stale version. Returns `ConsentDetails` |
| POST | `/{consentId}/conditions` | CanWrite | 404 if the consent is unknown; 400 on `projectId` if the project is archived. Returns `201 ConditionDetails` |
| PUT | `/{consentId}/conditions/{conditionId}` | CanWrite | Fields + `version`; 409 on a stale version |
| POST | `/{consentId}/conditions/{conditionId}/complete` | CanWrite | Body `{ version, completedOn }`. `completedOn` is required and can't be after today (400 on `completedOn`). Already complete → 200 unchanged |
| POST | `/{consentId}/conditions/{conditionId}/reopen` | CanWrite | Body `{ version }`. Not complete → 200 unchanged |

Cross-project: `GET /api/consents/due` (CanRead) returns `DueItem[]` for active consents on non-archived projects:
- every **condition** whose state is `Overdue` or `DueSoon`
- every **consent** whose `ExpiryState` is `Overdue` or `DueSoon`

`DueItem` has these fields:

| Field | Notes |
|---|---|
| `kind` | `Condition` or `Expiry` |
| `projectId`, `projectCode`, `projectName` | |
| `consentId`, `consentType`, `consentTitle`, `consentReference` | |
| `conditionId?`, `conditionReference?` | Condition items only |
| `description` | Condition text, or "Consent expires" |
| `date` | |
| `state` | |

Order: date ascending, then project code. Project names come from one `IPortfolioQueries.GetSummaries` call.

`ConsentDetails` has these fields:
- all consent fields
- `isActive`
- `expiryState`, `attention`
- the four counts
- `conditions: ConditionDetails[]`
- the audit fields and `version`

`ConditionDetails` has all condition fields, `dueState`, the audit fields and `version`.

Errors follow the ProblemDetails conventions: 400 with field keys, 404, and 409 with "This consent/condition was changed by someone else."

## 5. UI

### 5.1 Project detail: Consents section (below Risks)

- **Card header:** "Consents" and "Add consent" (canWrite and project not archived).
- **Consent list:** an Accordion, one item per consent.
  - **Control row:** type label, title, reference, status badge, attention badge (Overdue red, Due soon yellow, nothing otherwise) and "n open of m conditions".
  - **Panel:** authority, lodged, granted and expiry dates (expiry with its badge: Expired / Expiring soon), notes, and an Edit button.
  - **Conditions table:** Ref, Description, Due, State badge, Actions (Edit; Complete or Reopen), plus "Add condition".
- **Consent modal:** Type, Title, Authority, Reference, Status, Lodged, Granted, Expiry, Notes. Server 400s map onto the fields; a 409 closes the modal with a notification and refreshes the list (same as Risks).
- **Condition modal:** Reference, Description, Due date.
- **Complete modal:** "Completed on" date, defaulting to today.
- **Empty states:** "No consents recorded." and, for conditions, "No conditions recorded."
- **Due-state badges:** Overdue red, Due soon yellow, Upcoming blue, Done green, None no badge.
- **Labels:** ResourceConsent "Resource consent", SubdivisionConsent "Subdivision consent", BuildingConsent "Building consent", Section224c "s224(c) certificate", CodeComplianceCertificate "Code compliance certificate (CCC)", Other "Other". InfoRequested displays as "Info requested (s92)".

### 5.2 Consents due page (`/consents`, nav "Consents")

- Title "Consents due" with the subtitle "Overdue and due within 30 days (consent expiries within 90 days) across active projects."
- A table: Date, State badge, Project (link `/projects/{id}`, "CODE · Name"), Consent (type label + title), Item (condition ref + description, or "Consent expires").
- Empty state: "Nothing overdue or due soon."

## 6. Reminders decision: deferred to v2

**v1 does not include the EventBridge → Lambda → email digest.** Why:

1. **It needs a second Lambda entry point.** The API Lambda is hosted by `AddAWSLambdaHosting(HttpApi)` and can't receive a scheduled event. A digest means either:
   - a new function project with its own publish artifact, which touches CI (`ci.yml`/`deploy*.yml` publish only `artifacts/api`); or
   - a mode switch in the shared `Program.cs`.

   Both widen the change to shared files that sibling module branches are also editing. The mode switch also risks the API's startup path in code we can't run locally.
2. **It can't be verified here.** Deploying is off-limits, and there's no local EventBridge/SNS. The only check would be `cdk synth`, which proves the wiring exists, not that the email ever arrives.
3. **Recipients are unresolved.** The existing SNS topic is for ops alarms. Project managers aren't in any recipient list, and Cognito has no "who manages which project" mapping (Project Manager is free text). Sending the digest to the alarm address would be noise to the wrong people.
4. **Most of the value ships anyway.** The "Consents due" page is exactly the digest's content, live and visible to everyone with access. The derivation is server-side, in one place (`ConsentRules` + the `due` query), so v2 can reuse it unchanged.

**v2 sketch (for its own spec):**
- An EventBridge Scheduler rule (weekdays 07:00 `Locale:TimeZone`) triggers a small digest Lambda.
- The Lambda reuses the module's due query (exposed as a public `IConsentsDigest` in `Contracts/`) and publishes one message to a new `<name>-consent-reminders` SNS topic.
- Recipients come from a new optional `consentReminderEmails` list in the deployment file. A sparse GSI on due date replaces the Scan if volume grows.

**Cost if wrong:** users must open the page to see what's due. The v2 work is additive; nothing in v1 has to change.

## 7. Out of scope (v1)
- Email or other reminders (see §6)
- Deleting consents or conditions
- Documents (consent decision PDFs, plans)
- Statutory processing-day clocks (e.g. 20 working days) and s37 extensions
- Links from consents to risks or contractors
- A per-deployment configurable type list
- Condition assignees
