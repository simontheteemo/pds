# Tech Stack — property-dev-system (PDS)

Living reference for what the system is built on and why. Change this file (and add an ADR in `docs/adr/`) when a choice changes.

Guiding principles:

1. **Lightweight frameworks.** Prefer the platform (ASP.NET Core, EF Core, React) over layers on top of it. Add a library only when it removes real code.
2. **Provider-managed infrastructure.** No servers, containers or clusters to patch. Everything scales down when idle.
3. **Single-tenant, tenant-ready.** One deployment (own AWS account + database) per client. No client names, branding or rules hard-coded — all per-deployment configuration.

## Backend

| Concern | Choice | Notes |
|---|---|---|
| Runtime | **.NET 10 (LTS)**, C# 14 | .NET 8 ends support 10 Nov 2026 — see ADR-0001 |
| Web | ASP.NET Core **Minimal APIs** | No MVC controllers, no mediator library |
| Architecture | **Modular monolith** | One project per module; `internal` everywhere except the module's `Contracts/` namespace |
| Data access | **EF Core 10** + **Npgsql** | One `DbContext` + schema per module; migrations per module |
| Validation | **FluentValidation** | One validator per command |
| Logging | **Serilog** → CloudWatch (JSON) | Correlation ID per request |
| API docs | Built-in `Microsoft.AspNetCore.OpenApi` | Generates the TypeScript client for `web/` |
| Lambda hosting | `Amazon.Lambda.AspNetCoreServer.Hosting` | Same app runs locally with Kestrel or on Lambda |

Deliberately **not** used (until a real need appears): MediatR, AutoMapper, message bus, outbox, CQRS read stores, repository/unit-of-work abstractions over EF Core.

## Frontend

| Concern | Choice |
|---|---|
| Build | **Vite** + **React 19** + **TypeScript** (strict) |
| Server state | **TanStack Query** |
| Routing | **React Router** |
| UI components | **Mantine** |
| Forms | Mantine Form + **Zod** |
| Auth | `react-oidc-context` (OIDC + PKCE against Cognito managed login) |
| API client | Generated from the backend OpenAPI document (`openapi-typescript` + `openapi-fetch`) |

## AWS (all managed)

| Concern | Service |
|---|---|
| API compute | **AWS Lambda** (.NET 10) behind **API Gateway HTTP API** |
| AuthN | **Cognito user pool** (managed login); JWT validated by the **API Gateway JWT authorizer** |
| AuthZ | Cognito groups (`Admin`, `Manager`, `Viewer`) → ASP.NET authorization policies |
| Database | **Aurora Serverless v2 PostgreSQL**, min capacity 0 (auto-pause) in non-prod |
| Secrets / config | **Secrets Manager** (DB credentials), **SSM Parameter Store** (settings, branding) |
| Frontend hosting | **S3 + CloudFront** (Origin Access Control) |
| Observability | **CloudWatch** logs, metrics, alarms |
| Scheduled work (later) | **EventBridge Scheduler** / Step Functions (consent & compliance reminders) |
| Documents (later) | **S3** with pre-signed URLs |
| IaC | **AWS CDK (C#)** in `infra/` |

## Tooling

| Concern | Choice |
|---|---|
| Source control / CI | GitHub + **GitHub Actions**, AWS access via **OIDC** (no stored keys) |
| Local dev | Docker Compose (**Postgres only**); dev auth bypass with a fixed user |
| Backend tests | **xUnit**, **Testcontainers** (Postgres), `WebApplicationFactory` |
| Frontend tests | **Vitest** + React Testing Library |
| Formatting / lint | `dotnet format`, ESLint + Prettier |
