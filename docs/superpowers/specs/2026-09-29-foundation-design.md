# PDS Foundation + Project Portfolio — Design Spec

- Date: 2026-09-29 (Rev B: DynamoDB)
- Author: @simon (with Claude)
- Status: **Approved design; spec revised for DynamoDB**
- Related: [Tech stack](../../tech-stack.md) · [ADR-0001 .NET 10](../../adr/0001-dotnet-10-lts.md) · [ADR-0002 DynamoDB](../../adr/0002-dynamodb.md) · [Architecture set](../../architecture/pds-architecture.html)

## 1. Purpose

Seine Project Limited is replacing spreadsheets, email and scattered project files with one web system for its property development portfolio. This spec covers the **first sub-project**:

1. **Foundation:** the codebase structure, auth, data access, infrastructure, CI/CD and app shell that every module builds on.
2. **Project Portfolio module:** the first vertical slice through that foundation. Every other module hangs off `Project`.

This is the **real MVP foundation**, not a throwaway prototype.

### What was stated vs assumed

| Stated by @simon | Assumed (correct me if wrong) |
|---|---|
| Six core modules: Portfolio, Risk, Consents, Contractors/Contracts, Costs, Sales | Each of the other modules gets its own spec → plan → build cycle |
| Stack: AWS, C#/.NET 10, React/TypeScript, modular monolith | Low user count (10–30 internal staff, < 5 req/s peak) |
| Lightweight frameworks; provider-managed infrastructure | Primary region `ap-southeast-2` (Sydney); Auckland `ap-southeast-6` if every service is available there |
| Lambda for the API; DynamoDB for data | Currency NZD, timezone Pacific/Auckland, both configurable |
| Single-tenant, tenant-ready (one deployment per client) | Repo folder is `pds/`; namespace prefix is `PDS` |
| Generic naming, reusable for other clients | MVP scope beyond Portfolio is decided after Miller Consulting's phased plan |

### Success criteria

- A user signs in through Cognito and can list, filter, view, create, edit, archive and restore projects in the deployed `dev` environment.
- The same code runs locally with `docker compose up` + `dotnet run` + `npm run dev`, with no AWS account.
- A merge to `main` passes CI and deploys to `dev`. Deploying to `prod` needs a manual approval, and prod receives the same build.
- Adding a module means copying the Portfolio shape. The host changes by one `AddXModule()` / `MapXEndpoints()` line each.
- No client-specific names, logos or rules appear in source code.

## 2. Scope

**In scope:** repo and solution structure; shared kernel; Portfolio module (domain, persistence, API, UI); Cognito authentication with Admin/Manager/Viewer roles; per-deployment configuration and branding; CDK infrastructure for `dev` and `prod`; CI/CD; automated tests.

**Out of scope (later sub-projects):**
- Risk, Consents, Contractors, Costs and Sales modules
- **Actual** costs on projects. Portfolio stores the budget only.
- Documents (S3), reminders, reporting and the Athena export, spreadsheet import
- A user-management UI (users are managed in the Cognito console)
- Multi-tenancy within one deployment

## 3. Architecture

### 3.1 Runtime shape (architecture set A-01)

```
Browser ──► CloudFront ──► S3 (SPA build + config.json)
   │
   ├──► Cognito hosted login (OIDC code + PKCE) ──► ID token
   │
   └──(Bearer ID token, CORS)──► API Gateway HTTP API ──(JWT authorizer)──► Lambda PDS.Api ──► DynamoDB tables
                                                                              └──► CloudWatch Logs
```

- There's no VPC. The Lambda reaches DynamoDB over the AWS SDK with IAM permissions scoped to its tables.
- The SPA reads `config.json` (API base URL) from its own bucket, then calls `GET /api/config` for branding, locale and auth settings. One build serves every deployment.
- The API uses the **ID token** as the bearer token, so it has the user's email and groups without an extra call. API Gateway validates the token's `aud` against the app client id.
- Configuration comes from the CDK deployment file and is passed to the Lambda as environment variables. There's no runtime config service.

### 3.2 Repository layout

```
pds/
├── src/
│   ├── PDS.Api/                   host: Program.cs, security, config endpoints, ProblemDetails, logging
│   ├── PDS.Shared/                typed IDs, Money, ICurrentUser, Dynamo helpers, ITableDefinition, validation filter
│   └── Modules/
│       └── PDS.Portfolio/
│           ├── Contracts/         PUBLIC: ProjectId, ProjectSummary, IPortfolioQueries
│           ├── Domain/            internal: Project, Site, ProjectStage, ProjectStatus
│           ├── Features/          internal: one file per use case (endpoint + request + validator + handler)
│           ├── Data/              internal: ProjectStore, ProjectItemMapper, PortfolioTable
│           └── PortfolioModule.cs public: AddPortfolioModule(), MapPortfolioEndpoints()
├── tests/
│   ├── PDS.Tests/                 unit + architecture tests
│   ├── PDS.IntegrationTests/      API tests against DynamoDB Local (Testcontainers)
│   └── PDS.Infra.Tests/           CDK assertion tests
├── web/                           React SPA
├── infra/PDS.Infra/               CDK app (C#)
├── docs/
├── docker-compose.yml             DynamoDB Local
├── global.json, Directory.Build.props, Directory.Packages.props, PDS.slnx
└── .github/workflows/
```

### 3.3 Module rules

1. A module is **one project**. Everything is `internal` except the `Contracts/` namespace and the `*Module.cs` registration class.
2. Modules reference each other **only through `Contracts/`**. An architecture test fails the build otherwise.
3. Each module **owns its DynamoDB table(s)**. No module reads or writes another module's table. A cross-module link stores the other module's ID, and existence is checked through that module's `Contracts` query.
4. Use cases are **vertical slices**. Handlers use the module's store class directly; there's no generic repository.
5. There's no event bus until a module needs async side-effects.

### 3.4 Shared kernel (`PDS.Shared`)

- Strongly-typed IDs (`readonly record struct ProjectId(Guid Value)`, UUIDv7)
- `Money(decimal Amount, string Currency)`
- `ICurrentUser` (id, name, email, roles)
- `TimeProvider` (built-in) for all timestamps
- `ITableDefinition`: logical name, key schema and GSIs. Local dev and tests use it to create tables; CDK creates the real ones. An infra test checks that the two match.
- `DynamoTableNames`: resolves the logical name (`portfolio`) to a physical name through the `Tables:<Logical>` config value, falling back to `pds-<logical>`
- `ValidationFilter<T>`: runs the FluentValidation validator and returns `400 ValidationProblemDetails` with camelCase dotted keys (`site.city`)
- Security constants: `Roles`, `Policies`

## 4. Project Portfolio module

### 4.1 Domain model

**`Project`**

| Field | Type | Rules |
|---|---|---|
| `Id` | `ProjectId` | UUIDv7, server-generated |
| `Code` | string | Required. Normalised to trimmed upper-case. Pattern `^[A-Z0-9][A-Z0-9-]{0,19}$`. Unique per deployment |
| `Name` | string | Required, ≤ 200 |
| `Site` | `Site` | Required; see below |
| `Stage` | `ProjectStage` | Required |
| `Status` | `ProjectStatus` | Required |
| `PlannedStart`, `PlannedCompletion` | `DateOnly?` | If both are set, completion ≥ start |
| `ActualStart`, `ActualCompletion` | `DateOnly?` | If both are set, completion ≥ start |
| `Budget` | `Money?` | Amount ≥ 0, ≤ 2 decimal places, in the deployment currency |
| `ProjectManager` | string? | ≤ 200 |
| `Description` | string? | ≤ 4000 |
| `IsArchived` | bool | Soft-archive only; no hard delete |
| `CreatedAt/By`, `UpdatedAt/By` | | Set by the store |
| `Version` | long | Starts at 1; +1 on every write |

**`Site`:** `AddressLine` (required, ≤ 200), `Suburb`, `City` (required, ≤ 100), `Region`, `Postcode`, `LegalDescription`, `TitleReference`, `LandAreaSqm` (≥ 0).

**`ProjectStage`:** `Acquisition, Feasibility, Design, Consenting, Construction, Sales, Completed`. Any change is allowed.
**`ProjectStatus`:** `OnTrack, AtRisk, Delayed, OnHold, Cancelled`.
Both are serialised as strings in JSON and in DynamoDB.

### 4.2 DynamoDB table `portfolio`

Key schema: `pk` (S, hash), `sk` (S, range). GSI `gsi1`: `gsi1pk` (S, hash), `gsi1sk` (S, range), projection ALL. On-demand billing, PITR on, deletion protection in prod.

| Item | `pk` | `sk` | `gsi1pk` | `gsi1sk` | Other attributes |
|---|---|---|---|---|---|
| Project | `PROJECT#<id>` | `PROJECT` | `PROJECT` | `<code>` | all fields above; `site` stored as a Map; dates as `yyyy-MM-dd` strings; timestamps as ISO-8601 UTC; `budgetAmount` (N) + `currency`; `version` (N) |
| Code guard | `CODE#<code>` | `CODE` | – | – | `projectId` |

| Access pattern | Operation |
|---|---|
| Get by id | `GetItem(pk, sk)` |
| List / filter / search | `Query gsi1 where gsi1pk = PROJECT` (read all pages), then filter and page in memory, ordered by code |
| Create | `TransactWriteItems`: Put project `attribute_not_exists(pk)` + Put code guard `attribute_not_exists(pk)` |
| Update, same code | `PutItem` with `version = :expected` |
| Update, code changed | `TransactWriteItems`: Put project (version check) + Delete old guard + Put new guard `attribute_not_exists(pk)` |
| Archive / restore | Same as "update, same code" |

A failed condition is resolved as follows. Missing item → 404. Version mismatch → 409. Code guard exists → 400 on `code` ("Code is already in use").

### 4.3 API

All routes sit under `/api/portfolio`. JSON uses camelCase. Errors are `application/problem+json`.

| Method | Route | Policy | Purpose / result |
|---|---|---|---|
| GET | `/projects?stage=&status=&search=&includeArchived=false&page=1&pageSize=25` | CanRead | `PagedResult<ProjectListItem>`. `pageSize` is clamped to 1–100 and `page` to ≥ 1. Search is a case-insensitive substring match on code, name, suburb and city |
| GET | `/projects/{id}` | CanRead | `ProjectDetails`, or 404 |
| POST | `/projects` | CanWrite | `201` + `Location` + `ProjectDetails` |
| PUT | `/projects/{id}` | CanWrite | Full replace; body includes `version`; `200` + `ProjectDetails` |
| POST | `/projects/{id}/archive` | CanAdminister | Body `{ version }`; `200` + `ProjectDetails` |
| POST | `/projects/{id}/restore` | CanAdminister | Body `{ version }`; `200` + `ProjectDetails` |

Host endpoints: `GET /api/config` (anonymous; branding, locale, auth settings) and `GET /api/me` (authenticated; id, name, email, roles).

### 4.4 Contracts for future modules

```csharp
public readonly record struct ProjectId(Guid Value);
public sealed record ProjectSummary(ProjectId Id, string Code, string Name, string Stage, string Status, bool IsArchived);
public interface IPortfolioQueries
{
    Task<bool> Exists(ProjectId id, CancellationToken ct);
    Task<IReadOnlyList<ProjectSummary>> GetSummaries(IReadOnlyCollection<ProjectId> ids, CancellationToken ct);
}
```

## 5. Cross-cutting concerns

### 5.1 Authentication and authorisation
- **Cloud:** the API Gateway JWT authorizer validates the ID token (issuer = the user pool, audience = the app client). The `Gateway` auth scheme builds the principal from the authorizer's JWT claims, which the Lambda adapter passes in the request context. Claims arriving on `HttpContext.User` are the fallback.
- **Group claims** can arrive as separate claims or as one bracketed string (`"[Admin Manager]"`). A claims transformation handles both shapes and maps them to role claims.
- **Policies:** CanRead = Viewer|Manager|Admin; CanWrite = Manager|Admin; CanAdminister = Admin.
- **Local:** `Auth:Mode=Development` signs in a fixed user whose roles come from config. Options validation **refuses to start** in this mode outside the Development environment.
- **API Gateway routes:** `GET /api/config` has no authorizer; `ANY /api/{proxy+}` requires the JWT.

### 5.2 Errors
- Validation → `400 ValidationProblemDetails`, with camelCase dotted keys the UI shows against fields
- Malformed JSON or an unknown enum value → `400` (never 500)
- Not found → `404`; version conflict → `409`, and the UI offers to reload
- Unhandled → `500` ProblemDetails with a `correlationId`. No exception details are returned outside Development.

### 5.3 Configuration and tenant-readiness
Per-deployment values: `Branding:ProductName`, `Branding:LogoUrl`, `Branding:PrimaryColor`, `Locale:Currency`, `Locale:Culture`, `Locale:TimeZone`, `Auth:Mode`, `Auth:Authority`, `Auth:ClientId`, `Auth:LogoutDomain`, `Tables:Portfolio`. CDK reads `infra/deployments/<name>.json` and sets these as Lambda environment variables. A new client needs a new deployment file and a new AWS account; no code changes.

### 5.4 Observability
Serilog writes compact JSON to stdout. Each request log carries `CorrelationId` (from the `X-Correlation-Id` header, or generated) and `UserName`. The correlation ID is echoed in the response header and in ProblemDetails. Alarms go to an SNS email topic: API 5xx, Lambda errors and Lambda throttles, plus a monthly AWS Budgets alert at 80% of the configured limit. DynamoDB failures surface as API 5xx.

## 6. Frontend

- **App shell:** a Mantine `AppShell`, sidebar navigation (Projects), and a header showing the configured product name and logo plus a user menu with the user's name, roles and sign-out.
- **Pages:**
  - Projects list: stage/status filters, a debounced search box, pagination, a status badge, and an archived toggle for Admins
  - Project detail: summary, site, planned vs actual timeline, budget. Edit, archive and restore actions are shown by role.
  - Create and edit: a shared form with Zod validation mirroring the server rules. Server `400` errors are mapped onto fields; a `409` shows a reload prompt.
- **Formatting:** money and dates go through `Intl`, using the configured culture and currency.
- The UI hides write actions from Viewers for convenience only; the server enforces all permissions.

## 7. Infrastructure (CDK, C#)

Per deployment (`<name>` = e.g. `seine-dev`):

| Stack | Contents |
|---|---|
| `<name>-data` | DynamoDB `<name>-portfolio` (on-demand, PITR, deletion protection + RETAIN in prod) |
| `<name>-auth` | Cognito user pool (email sign-in, self-signup off), groups `Admin`/`Manager`/`Viewer`, hosted-UI domain. RETAIN in prod |
| `<name>-app` | API Lambda (.NET 10, arm64, 1024 MB, 30 s), HTTP API + JWT authorizer + CORS, user pool client, S3 + CloudFront, SPA upload with generated `config.json`, alarms + SNS, monthly budget |
| `<name>-github-oidc` (deployed by hand once per account) | GitHub OIDC provider + deploy role trusted only by this repo's matching GitHub environment |

## 8. Testing strategy

| Layer | What | Tooling |
|---|---|---|
| Unit | Domain rules, validators, item mapper round-trip, claims transformation, Money | xUnit |
| Architecture | Modules only use other modules' `Contracts`; `Domain` has no ASP.NET or AWS SDK dependencies | xUnit + reflection |
| Integration | Every endpoint: happy path, validation, 404, 409, duplicate code, role enforcement, paging bounds | `WebApplicationFactory` + Testcontainers DynamoDB Local |
| Frontend | List filters, form validation, server-error mapping, role-based visibility | Vitest + RTL + MSW |
| Infra | Table keys match `ITableDefinition`; prod retention and deletion protection; JWT authorizer on the proxy route; no VPC | xUnit + `Amazon.CDK.Assertions` |

## 9. CI/CD (GitHub Actions)

- **`ci.yml`** (every PR and push): `dotnet format --verify-no-changes` → build → tests → web lint, typecheck, test and build → publish the Lambda → `cdk synth`.
- **`deploy.yml`** (push to `main`): build once → deploy to `dev` → smoke test (`GET /api/config` returns 200) → a GitHub Environment approval → deploy the **same artifacts** to `prod`.
- AWS access uses GitHub OIDC → a per-account deploy role.

## 10. Risks and open items

| # | Item | Handling |
|---|---|---|
| 1 | Lambda managed runtime for .NET 10 in the chosen region | Verify during infra work. Fallback: a container-image Lambda |
| 2 | Region: Sydney vs Auckland | Check service availability. The deployment file sets the region |
| 3 | In-memory list filtering | Fine up to a few thousand projects; revisit with per-filter GSIs |
| 4 | Reporting without SQL | Running totals now; the Athena export in the reporting sub-project |
| 5 | Lambda cold start (~1 s) | Accept; ReadyToRun publish |
| 6 | GitHub repo and AWS accounts not yet set up | Local build and tests aren't blocked; the deploy tasks are |

## 11. Roadmap (each item gets its own spec)

1. Risk Registry → 2. Contractors & Contracts → 3. Cost Tracking (running totals) → 4. Consents (+ EventBridge reminders) → 5. Sales Pipeline → 6. Documents, reporting export, cashflow.
