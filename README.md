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
dotnet test                  # unit and integration (needs Docker)
```

Frontend tests are not set up yet.

## API contract

`dotnet build` (Debug) regenerates `web/src/shared/api/openapi.json`; `cd web && npm run gen:api` regenerates the TypeScript types. Commit both. CI fails if either is out of date.

## Deploying

Each client environment is its own AWS account. `dev` and `prod` are separate accounts, and so is each client.

### First deployment of an environment (once per account)

1. **Pick the region.** Use `ap-southeast-6` (Auckland) only if Lambda (`dotnet10` runtime), API Gateway HTTP APIs, DynamoDB, Cognito, CloudFront and Budgets are all available there; otherwise use `ap-southeast-2` (Sydney).
2. **Create the deployment file.** Copy `infra/deployments/example-dev.json` to `infra/deployments/<name>.json` (for example `seine-dev`). Set `account`, `region`, `isProduction`, a globally unique `cognitoDomainPrefix`, `alarmEmail`, `monthlyBudgetUsd`, branding and `gitHub` (`repository` = `org/repo`, `environment` = `dev` or `prod`). Commit it.
3. **Build the artifacts** that synthesis needs:
   ```bash
   dotnet publish src/PDS.Api -c Release -r linux-arm64 --self-contained false -p:PublishReadyToRun=true -o artifacts/api
   (cd web && npm ci && npm run build)
   ```
4. **Bootstrap CDK and create the GitHub deploy role**, using administrator credentials for that account:
   ```bash
   cd infra
   npx aws-cdk@2.1143.0 bootstrap aws://<account>/<region> -c deployment=<name>
   npx aws-cdk@2.1143.0 deploy <name>-github-oidc -c deployment=<name>
   ```
   Note the `DeployRoleArn` output. If the account already has a GitHub OIDC provider, remove the provider from `GitHubOidcStack` and import the existing one instead.
5. **Configure GitHub.** Create the environments `dev` and `prod`, and give `prod` required reviewers. In each environment, set these variables: `AWS_DEPLOY_ROLE_ARN` (from step 4), `AWS_REGION`, and `PDS_DEPLOYMENT` (the `<name>`).
6. **Deploy** by pushing to `main`, or by running the `deploy` workflow manually.
7. **Create the first administrator.** Use the `UserPoolId` output of `<name>-app`:
   ```bash
   aws cognito-idp admin-create-user --user-pool-id <pool> --username admin@example.com \
     --user-attributes Name=email,Value=admin@example.com Name=email_verified,Value=true Name=name,Value="First Admin"
   aws cognito-idp admin-add-user-to-group --user-pool-id <pool> --username admin@example.com --group-name Admin
   ```
8. **Check it works.** Open the `SiteUrl` output, sign in with the temporary password from the invitation email, and create a project.

### Adding a user

Create the user in the Cognito console (or with `admin-create-user`) and add them to exactly one of the groups `Viewer`, `Manager` or `Admin`. A signed-in user with no group sees "No access yet".
