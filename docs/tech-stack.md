# Tech Stack — property-dev-system (PDS)

This is the living reference for what the system is built on and why. When a choice changes, update this file and add an ADR in `docs/adr/`.

Guiding principles:

1. **Lightweight frameworks.** Prefer the platform (ASP.NET Core, the AWS SDK, React) over layers on top of it. Add a library only when it removes real code.
2. **Provider-managed infrastructure.** No servers, containers, clusters or networks to manage. Everything scales down to near-zero when idle.
3. **Single-tenant, tenant-ready.** Each client gets its own deployment (its own AWS account and tables). No client names, branding or rules are hard-coded; all of that is per-deployment configuration.

## Backend

| Concern | Choice | Notes |
|---|---|---|
| Runtime | **.NET 10 (LTS)**, C# 14 | ADR-0001 |
| Web | ASP.NET Core **Minimal APIs** | No MVC controllers, no mediator library |
| Architecture | **Modular monolith** | One project per module; `internal` everywhere except the module's `Contracts/` namespace |
| Data | **DynamoDB** via `AWSSDK.DynamoDBv2` low-level client | One table per module; hand-written item mappers; no ORM (ADR-0002) |
| Validation | **FluentValidation** | One validator per command |
| Logging | **Serilog** → CloudWatch (compact JSON) | Correlation ID per request |
| API docs | Built-in `Microsoft.AspNetCore.OpenApi` + build-time document generation | Feeds the TypeScript client in `web/` |
| Lambda hosting | `Amazon.Lambda.AspNetCoreServer.Hosting` | The same app runs locally on Kestrel or on Lambda |

Deliberately **not** used until a real need appears: EF Core or any ORM, MediatR, AutoMapper, a message bus or outbox, repository/unit-of-work abstractions, and DynamoDB Streams.

## Frontend

| Concern | Choice |
|---|---|
| Build | **Vite** + **React 19** + **TypeScript** (strict) |
| Server state | **TanStack Query** |
| Routing | **React Router** |
| UI components | **Mantine** |
| Forms | Mantine Form, validated with **Zod** through a small in-house helper (no resolver package) |
| Auth | `react-oidc-context` (OIDC authorization code + PKCE against Cognito) |
| API client | Generated from the backend OpenAPI document (`openapi-typescript` + `openapi-fetch`) |

## AWS (all managed, no VPC)

| Concern | Service |
|---|---|
| API compute | **AWS Lambda** (.NET 10, arm64) behind **API Gateway HTTP API** (CORS enabled) |
| AuthN | **Cognito user pool** + hosted login; the JWT is validated by the **API Gateway JWT authorizer** |
| AuthZ | Cognito groups (`Admin`, `Manager`, `Viewer`) → ASP.NET authorization policies |
| Database | **DynamoDB**, one table per module, on-demand, PITR on, deletion protection in prod |
| Config | CDK deployment file → **Lambda environment variables**; the SPA reads `config.json` from its bucket |
| Frontend hosting | **S3 + CloudFront** (Origin Access Control) |
| Observability | **CloudWatch** logs, metrics, alarms → SNS email |
| Scheduled work (later) | **EventBridge Scheduler** (consent and compliance reminders) |
| Documents (later) | **S3** with pre-signed URLs |
| Reporting (later) | DynamoDB export to S3 + **Athena** |
| IaC | **AWS CDK (C#)** in `infra/` |

## Tooling

| Concern | Choice |
|---|---|
| Source control / CI | GitHub + **GitHub Actions**; AWS access via **OIDC** (no stored keys) |
| Local dev | Docker Compose running **DynamoDB Local**; a dev auth mode signs in a fixed user |
| Backend tests | **xUnit**, **Testcontainers** (DynamoDB Local), `WebApplicationFactory` |
| Frontend tests | **Vitest** + React Testing Library + MSW |
| Formatting / lint | `dotnet format`, ESLint + Prettier |
