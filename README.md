# property-dev-system (PDS)

Web system for managing a property development portfolio. It's single-tenant: one deployment (one AWS account) per client.

- Design: `docs/superpowers/specs/2026-09-29-foundation-design.md`
- Architecture set: `docs/architecture/pds-architecture.html`
- Decisions: `docs/adr/`
- Stack: `docs/tech-stack.md`

## Local development

Prerequisites: .NET 10 SDK, Node.js ≥ 22.12, Docker.

```bash
docker compose up -d dynamodb          # DynamoDB Local on :8000
dotnet run --project src/PDS.Api       # API on http://localhost:5080, signed in as the dev user (Admin)
cd web && npm install && npm run dev   # SPA on http://localhost:5173 (proxies /api to :5080)
```

To try another role locally, change `Auth:DevUser:Roles` in `src/PDS.Api/appsettings.Development.json`.

## Tests

```bash
dotnet test                  # unit, architecture, integration (needs Docker), CDK assertions
cd web && npm test           # frontend
```

## API contract

`dotnet build` (Debug) regenerates `web/src/shared/api/openapi.json`; `cd web && npm run gen:api` regenerates the TypeScript types. Commit both. CI fails if either is out of date.
