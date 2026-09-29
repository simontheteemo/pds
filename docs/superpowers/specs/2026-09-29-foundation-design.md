# PDS Foundation + Project Portfolio — Design Spec

- Date: 2026-09-29
- Author: @simon (with Claude)
- Status: **Draft for review**
- Related: [Tech stack](../../tech-stack.md) · [ADR-0001 .NET 10](../../adr/0001-dotnet-10-lts.md)

## 1. Purpose

Seine Project Limited is replacing spreadsheets, email and scattered project files with one web system for its property development portfolio. This spec covers the **first sub-project**:

1. **Foundation:** the codebase structure, auth, database, infrastructure, CI/CD and app shell that every module builds on.
2. **Project Portfolio module:** the first vertical slice through that foundation. Every other module hangs off `Project`.

This is the **real MVP foundation**, not a throwaway. Code quality, tests and deployability matter from day one.

### What was stated vs assumed

| Stated by @simon | Assumed (correct me if wrong) |
|---|---|
| Six core modules: Portfolio, Risk, Consents, Contractors/Contracts, Costs, Sales | The other five modules each get their own spec → plan → build cycle after this one |
| Stack: AWS, C#/.NET, React/TypeScript, modular monolith | .NET 10 LTS instead of 8 (ADR-0001) |
| Lightweight frameworks; provider-managed infrastructure | Low user count (an internal team, tens of users), so Lambda cold starts are acceptable |
| Lambda for the API | NZ-based users; primary region `ap-southeast-2` (Sydney), or `ap-southeast-6` (Auckland) if it has every service we need |
| Single-tenant, tenant-ready (one deployment per client) | Currency NZD, timezone Pacific/Auckland, both configurable per deployment |
| Generic naming (`property-dev-system`), reusable for other clients | Repo folder is `pds/`; product and namespace prefix is `PDS` |
| First slice = Project Portfolio | MVP scope beyond Portfolio is decided after Miller Consulting's phased plan |

### Success criteria

- A user signs in through Cognito and can view, filter, create and edit projects in the deployed `dev` environment.
- The same code runs locally with `docker compose up` + `dotnet run` + `npm run dev`, with no AWS account needed.
- A merge to `main` passes CI (build, tests, architecture tests, lint) and deploys to `dev`. Deploying to `prod` needs a manual approval.
- Adding a second module means copying the Portfolio module's shape. No changes to the host beyond one `AddXModule()` / `MapXEndpoints()` line each.
- No client-specific names, logos or rules appear anywhere in source code.

## 2. Scope

**In scope**
- Repo, solution and module structure; shared kernel
- Portfolio module: domain, persistence, API, UI (list, detail, create, edit)
- Authentication (Cognito) and role-based authorisation (Admin / Manager / Viewer)
- Per-deployment configuration and branding
- AWS infrastructure via CDK for `dev` and `prod` environments
- CI/CD with GitHub Actions
- Automated tests at unit, integration, architecture and frontend-component level

**Out of scope (later sub-projects)**
- Risk, Consents, Contractors, Costs and Sales modules
- **Actual** cost figures on projects. Portfolio stores the budget; actuals arrive with the Costs module through a `Contracts` query.
- Documents / S3 uploads, reminders / scheduling, reporting dashboard, data import from existing spreadsheets
- User management UI (users are managed in the Cognito console for now)
- Multi-tenancy inside one deployment

## 3. Architecture

### 3.1 Shape

```
Browser ──► CloudFront ──► S3 (React SPA)
   │
   └─(JWT)─► API Gateway HTTP API ──(JWT authorizer: Cognito)──► Lambda (PDS.Api, .NET 10)
                                                                    │  (VPC, private subnets)
                                                                    ├─► Aurora Serverless v2 PostgreSQL
                                                                    └─► Secrets Manager (VPC endpoint)
```

- **No NAT gateway.** API Gateway validates JWTs, so the Lambda never has to fetch Cognito keys. Secrets Manager is reached through a VPC interface endpoint. Logs go through the Lambda service itself.
- The ASP.NET Core app is identical locally (Kestrel) and in AWS (`AddAWSLambdaHosting(LambdaEventSource.HttpApi)`).

### 3.2 Repository layout

```
pds/
├── src/
│   ├── PDS.Api/                   host: Program.cs, auth, config, module registration, ProblemDetails
│   ├── PDS.Shared/                Entity base, typed IDs, Money, Result, ICurrentUser, IClock, EF conventions
│   └── Modules/
│       └── PDS.Portfolio/
│           ├── Contracts/         PUBLIC: ProjectId, ProjectSummary, IPortfolioQueries
│           ├── Domain/            internal: Project, Site, ProjectStage, ProjectStatus
│           ├── Features/          internal: one file per use case (endpoint + request + handler + validator)
│           ├── Data/              internal: PortfolioDbContext, configurations, Migrations/
│           └── PortfolioModule.cs public: AddPortfolioModule(), MapPortfolioEndpoints()
├── tests/
│   ├── PDS.Tests/                 unit + architecture tests
│   └── PDS.IntegrationTests/      API tests against real Postgres (Testcontainers)
├── web/                           React SPA
├── infra/                         CDK app (C#)
├── docs/
├── docker-compose.yml             Postgres 17
├── Directory.Build.props          shared: net10.0, nullable, warnings-as-errors, analyzers
├── Directory.Packages.props       central package versions
└── .github/workflows/
```

### 3.3 Module rules

1. A module is **one project**. Everything is `internal` except the `Contracts/` namespace and the `*Module.cs` registration class.
2. A module may reference another module **only through its `Contracts/` types**. An architecture test (plain reflection, no library) fails the build if a module uses a non-contract type from another module.
3. Each module owns a **Postgres schema** (`portfolio`, later `risks`, `costs`, …) and its own `DbContext` and migrations. **No foreign keys across schemas.** A cross-module link stores the other module's typed ID (e.g. `ProjectId`) and checks it exists through that module's `Contracts` query.
4. Use cases are **vertical slices**: one file holds the endpoint mapping, the request/response records, the validator and the handler. Handlers use the `DbContext` directly, with no repository layer.
5. Cross-module reads go through query interfaces in `Contracts/`, e.g. `IPortfolioQueries.GetSummaries(IEnumerable<ProjectId>)`. There's no event bus until a module needs async side-effects.

### 3.4 Shared kernel (`PDS.Shared`) — kept deliberately small

- `Entity<TId>` with audit fields (`CreatedAt/By`, `UpdatedAt/By`) set automatically by a `SaveChanges` interceptor
- Strongly-typed IDs (`readonly record struct ProjectId(Guid Value)`), using UUIDv7 for index-friendly ordering
- `Money(decimal Amount, string Currency)` value object; the currency defaults to the configured deployment currency
- `ICurrentUser` (from JWT claims, or the fixed dev user locally), `IClock`
- EF conventions: snake_case naming, `numeric(18,2)` for money, Postgres `xmin` as the concurrency token

## 4. Project Portfolio module

### 4.1 Domain model

**`Project` (aggregate root)**

| Field | Type | Rules |
|---|---|---|
| `Id` | `ProjectId` | UUIDv7 |
| `Code` | string(20) | Required, unique, e.g. `PDS-001` (user-entered) |
| `Name` | string(200) | Required |
| `Site` | `Site` (owned) | See below |
| `Stage` | `ProjectStage` | Required |
| `Status` | `ProjectStatus` | Required |
| `PlannedStart`, `PlannedCompletion` | `DateOnly?` | Completion ≥ start |
| `ActualStart`, `ActualCompletion` | `DateOnly?` | Completion ≥ start |
| `Budget` | `Money?` | ≥ 0 |
| `ProjectManager` | string(200)? | Free text for now; becomes a user reference later |
| `Description` | string(4000)? | |
| `IsArchived` | bool | Archived projects are hidden by default; nothing is ever hard-deleted |
| audit fields, `Version` (xmin) | | |

**`Site` (owned value object):** `AddressLine`, `Suburb`, `City`, `Region`, `Postcode`, `LegalDescription`, `TitleReference`, `LandAreaSqm` (decimal?). Only `AddressLine` and `City` are required.

**`ProjectStage`** (lifecycle, ordered): `Acquisition → Feasibility → Design → Consenting → Construction → Sales → Completed`. Any stage change is allowed; the UI shows it as a stepper.

**`ProjectStatus`** (health): `OnTrack`, `AtRisk`, `Delayed`, `OnHold`, `Cancelled`.

Stage and status are stored as strings so the database stays readable.

### 4.2 API

All routes sit under `/api/portfolio`. JSON uses camelCase. Errors are `application/problem+json`.

| Method | Route | Role | Purpose |
|---|---|---|---|
| GET | `/projects?stage=&status=&search=&includeArchived=&page=&pageSize=` | Viewer+ | Paged list (search on code, name, suburb, city) |
| GET | `/projects/{id}` | Viewer+ | Detail |
| POST | `/projects` | Manager+ | Create → `201` + `Location` |
| PUT | `/projects/{id}` | Manager+ | Full update; requires `version` (optimistic concurrency) |
| POST | `/projects/{id}/archive` | Admin | Archive |
| POST | `/projects/{id}/restore` | Admin | Restore |

Plus `GET /api/me` (current user and roles) and `GET /api/config` (branding, currency, locale). Both are in the host.

### 4.3 Contracts exposed to future modules

```csharp
public readonly record struct ProjectId(Guid Value);
public sealed record ProjectSummary(ProjectId Id, string Code, string Name, string Stage, string Status);
public interface IPortfolioQueries
{
    Task<bool> Exists(ProjectId id, CancellationToken ct);
    Task<IReadOnlyList<ProjectSummary>> GetSummaries(IEnumerable<ProjectId> ids, CancellationToken ct);
}
```

## 5. Cross-cutting concerns

### 5.1 Authentication and authorisation
- **Cloud:** the SPA signs in via Cognito managed login (OIDC authorization code + PKCE). API Gateway's JWT authorizer rejects invalid tokens. The app reads claims from the validated token and maps `cognito:groups` to roles.
- **Policies:** `Viewer` (read), `Manager` (read + write), `Admin` (everything + archive/restore). Role inclusion: Admin ⊃ Manager ⊃ Viewer.
- **Local:** with `Auth:Mode=Development`, a fixed dev user is signed in with a role chosen in `appsettings.Development.json`. This mode is **refused at startup** outside the Development environment.

### 5.2 Errors
- Validation failure → `400` with `ValidationProblemDetails` (field-keyed errors, shown against form fields in the UI)
- Not found → `404`; concurrency conflict (stale `version`) → `409`, and the UI offers to reload
- Unhandled → `500` with a correlation ID. Details are logged but never returned to the client.

### 5.3 Configuration and tenant-readiness
- Per-deployment settings, read from SSM Parameter Store in AWS and from `appsettings` locally: `Branding:ProductName`, `Branding:LogoUrl`, `Branding:PrimaryColor`, `Locale:Currency` (NZD), `Locale:TimeZone` (Pacific/Auckland), `Locale:Culture` (en-NZ).
- The SPA gets these from `GET /api/config` at start-up, so the same build artifact serves any client.
- CDK takes a `deploymentName` + `environment` (e.g. `seine-dev`, `seine-prod`) and names every resource from them. A new client means a new AWS account and a new CDK context. No code changes.

### 5.4 Data and migrations
- Each module's migrations run in module order through a **migrator Lambda** (the same assembly with a separate handler). The deploy workflow invokes it after `cdk deploy`. The API Lambda never migrates on start-up.
- Locally, `dotnet run` applies pending migrations automatically in Development only.
- Aurora automated backups: 7 days in dev, 35 days in prod. Prod has deletion protection.

### 5.5 Observability
- Serilog writes structured JSON to stdout, which Lambda sends to CloudWatch. Every log line carries the request correlation ID and user ID.
- CloudWatch alarms (prod): API 5xx rate, Lambda errors/throttles, Aurora CPU/ACU at maximum. Alerts go to an SNS email topic.

## 6. Frontend

- **App shell:** a Mantine `AppShell` with a sidebar nav. Only Portfolio is active; future modules appear only once they exist. The header shows the product name and logo from config, plus a user menu with sign-out.
- **Pages:**
  - Projects list: a table with stage/status filters, search, pagination, and colour-coded status badges
  - Project detail: summary, site, timeline (planned vs actual), budget
  - Create/edit form: Zod validation that mirrors the server rules; server `400` errors are mapped onto fields
- **Structure:** `web/src/features/portfolio/…` (pages, components, hooks), `web/src/shared/…` (api client, auth, layout, formatting)
- **Formatting:** money and dates use `Intl` with the configured locale and currency.
- **Role-aware UI:** write actions are hidden for Viewers. The server still enforces this; the UI hiding is only for convenience.

## 7. Infrastructure (CDK, C#)

Four stacks per deployment:

| Stack | Contents |
|---|---|
| `Network` | VPC (2 AZs, private isolated subnets, **no NAT**), Secrets Manager VPC endpoint, security groups |
| `Data` | Aurora Serverless v2 Postgres cluster (dev: 0–2 ACU with auto-pause; prod: 0.5–4 ACU), DB secret |
| `Auth` | Cognito user pool, groups, app client, managed login domain |
| `App` | API + migrator Lambdas, HTTP API + JWT authorizer, S3 + CloudFront for the SPA, SSM parameters, alarms |

Environments: `dev` and `prod`, **ideally in separate AWS accounts**.

## 8. Testing strategy

| Layer | What | Tooling |
|---|---|---|
| Unit | Domain rules (dates, budget, stage), validators, Money | xUnit |
| Architecture | Modules only use other modules' `Contracts`; `Domain` has no EF/ASP.NET dependencies | xUnit + reflection |
| Integration | Every endpoint: happy path, validation, 404, 409, role enforcement | `WebApplicationFactory` + Testcontainers Postgres |
| Frontend | List filtering, form validation and server-error mapping, role-based visibility | Vitest + RTL, API mocked with MSW |
| Infra | CDK synth + snapshot/assertion tests (no NAT, deletion protection in prod, etc.) | xUnit + `Amazon.CDK.Assertions` |

End-to-end browser tests are deferred until two or more modules exist.

## 9. CI/CD (GitHub Actions)

- **`ci.yml`** (every PR and push): restore → `dotnet format --verify-no-changes` → build (warnings as errors) → tests (Testcontainers on the runner) → `web`: lint, typecheck, test, build → `cdk synth`.
- **`deploy.yml`** (on `main`): build artifacts → `cdk deploy` to **dev** → run the migrator → upload the SPA to S3 + invalidate CloudFront → smoke test (`GET /api/config`). **Prod** uses the same steps behind a GitHub Environment approval.
- AWS access uses GitHub OIDC → a per-account IAM deploy role. No long-lived keys.

## 10. Risks and open items

| # | Item | Impact | Proposed handling |
|---|---|---|---|
| 1 | **.NET 10 vs .NET 8** (ADR-0001) | Runtime choice for everything | Confirm .NET 10; install the SDK |
| 2 | Region: Sydney vs Auckland (`ap-southeast-6`) | Latency, data residency | Check that Aurora Serverless v2, Cognito managed login and Lambda .NET 10 are available in Auckland; otherwise use Sydney |
| 3 | Lambda cold starts (~1–2 s after idle) | First request feels slow | Accept for an internal tool; ReadyToRun compilation; move to Fargate only if it becomes a real problem |
| 4 | Aurora auto-pause resume (~15 s) in dev | First request after a long idle is slow | Dev only. Prod keeps min 0.5 ACU (roughly NZ$60–80/month) |
| 5 | GitHub org/repo and AWS accounts not yet set up | Blocks the deploy pipeline | Local build and tests aren't blocked; the deploy tasks wait on these |
| 6 | Project Code format | Minor | User-entered, unique. Auto-numbering is deferred |
| 7 | Relationship to Miller Consulting's proposal | Their architecture recommendation may differ | This foundation is a reference point for that conversation; the ADRs record the reasoning |

## 11. Roadmap after this spec (each gets its own spec)

1. **Risk Registry:** risks linked to `ProjectId` (and later to consents/contractors), with a likelihood × impact matrix
2. **Contractors & Contracts:** register, contract terms, insurance/licence expiry
3. **Cost Tracking:** budget lines, actuals by category and contract; feeds budget vs actual back to Portfolio
4. **Consents:** consents, conditions, RFIs, expiry; EventBridge Scheduler reminders
5. **Sales Pipeline:** units/lots, listed → settled, feeds cashflow
6. Cross-cutting later: Documents (S3), reporting dashboard, cashflow forecasting
