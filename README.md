# property-dev-system (PDS)

Web system for managing a property development portfolio. It's single-tenant: one deployment (one AWS account) per client.

- Design: `docs/superpowers/specs/2026-09-29-foundation-design.md`
- Architecture set: `docs/architecture/pds-architecture.html`
- Decisions: `docs/adr/`
- Stack: `docs/tech-stack.md`

## Modules

One project per module under `src/Modules/`, one feature folder under `web/src/features/`. Each has its own spec.

- **Portfolio**: projects, stage, status and budget, plus programme milestones. [Foundation spec](docs/superpowers/specs/2026-09-29-foundation-design.md)
- **Risks**: per-project risk registry with ratings. [Spec](docs/superpowers/specs/2026-09-30-risk-registry-design.md)
- **Contractors**: contractors and their contracts. [Spec](docs/superpowers/specs/2026-09-30-contractors-design.md)
- **Costs**: cost lines, commitments and actuals with running totals. [Spec](docs/superpowers/specs/2026-10-14-costs-design.md)
- **Consents**: consents and their conditions, plus a "due" view. [Spec](docs/superpowers/specs/2026-10-14-consents-design.md)
- **Sales**: sales units and sale details, with a pipeline summary. [Spec](docs/superpowers/specs/2026-10-14-sales-design.md)
- **Documents**: versioned files per project, uploaded straight to S3 with presigned URLs. [Spec](docs/superpowers/specs/2026-10-14-documents-design.md)
- **Cashflow**: a forecast calculated from costs and sales; it stores nothing. [Spec](docs/superpowers/specs/2026-10-14-cashflow-design.md)
- **Reporting**: CSV exports of module data (UTF-8 with BOM, for Excel). [Spec](docs/superpowers/specs/2026-10-14-reporting-design.md)

## Local development

Prerequisites: .NET 10 SDK, Node.js ≥ 22.12, Docker.

```bash
docker compose up -d dynamodb          # DynamoDB Local on :8000
dotnet run --project src/PDS.Api       # API on http://localhost:5080, signed in as the dev user (Admin)
cd web && npm install && npm run dev   # SPA on http://localhost:5173 (proxies /api to :5080)
```

To try another role locally, change `Auth:DevUser:Roles` in `src/PDS.Api/appsettings.Development.json`. Dev auth signs every request in as that user, so no token is needed.

Per-module local settings, all already set in `appsettings.Development.json`:

- **Tables**: `Dynamo:CreateTablesOnStartup` creates every module's tables in DynamoDB Local on first run.
- **Documents**: `Documents:Storage=Local` replaces S3 with files on disk plus signed stand-ins for the presigned PUT/GET URLs (`/api/documents/local-files/...`). Files go to `<temp>/pds-documents`; set `Documents:LocalPath` to change that. The API refuses to start with `Local` outside the `Development` environment, so a deployed Lambda always uses S3 (`Documents__BucketName` is set by CDK).
- **Portfolio**: `Portfolio:StandardMilestones` (a list of `{ name, category }`) optionally overrides the default NZ milestone set.
- **Reporting, Cashflow, Sales, Consents, Costs, Risks, Contractors**: nothing to configure.

The shared `Locale` settings (currency, culture, time zone) live in `appsettings.json`. The API fails at startup if `Locale:TimeZone` is not a valid time zone.

## Tests

```bash
dotnet test                  # unit and integration (needs Docker)
```

```bash
cd web && npm test           # frontend unit tests (vitest)
```

## API contract

`dotnet build` (Debug) regenerates `web/src/shared/api/openapi.json`; `cd web && npm run gen:api` regenerates the TypeScript types. Commit both. CI fails if either is out of date.

## Deploying

Each client environment is its own AWS account. `dev` and `prod` are separate accounts, and so is each client.

### First deployment of an environment (once per account)

1. **Pick the region.** Use `ap-southeast-6` (Auckland) only if Lambda (`dotnet10` runtime), API Gateway HTTP APIs, DynamoDB, Cognito, CloudFront and Budgets are all available there; otherwise use `ap-southeast-2` (Sydney).
2. **Create the deployment file.** Copy `infra/deployments/example-dev.json` to `infra/deployments/<name>.json` (for example `seine-dev`). Set `account`, `region`, `isProduction`, a globally unique `cognitoDomainPrefix`, `alarmEmail`, `monthlyBudgetUsd`, branding, `gitHub` (`repository` = `org/repo`, `environment` = `dev` or `prod`) and optionally `consentReminders` (see below). Commit it.
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
8. **Check it works.** Run the smoke check, then do the manual checks below.

### After the first deploy (and after any deploy)

```bash
scripts/smoke-check.sh <SiteUrl> [<ApiUrl>]                     # ApiUrl defaults to apiBaseUrl in /config.json
PDS_TOKEN=<id token> scripts/smoke-check.sh <SiteUrl>           # adds the signed-in checks
PDS_TOKEN=<id token> SMOKE_DOCUMENTS=1 SMOKE_PROJECT_ID=<id> scripts/smoke-check.sh <SiteUrl>   # adds an upload round trip
```

Needs bash, curl and jq. It prints one PASS/FAIL/SKIP line per check and exits non-zero if anything failed.

- **Without a token**: the SPA loads, `/config.json` is served, `GET /api/config` returns 200 (proves the Lambda started, so `Locale:TimeZone` and the other settings resolved), and a protected endpoint returns 401.
- **With `PDS_TOKEN`** (a Cognito ID token, not the access token, for an Admin or Manager user): `/api/me`, one list endpoint per module, and a Reporting CSV that starts with a UTF-8 BOM. Get a token by signing in to the SPA and copying `id_token` from the `oidc.user:...` entry in the browser's session storage. The token is never printed.
- **With `SMOKE_DOCUMENTS=1`**: requests an upload URL, PUTs a tiny `.txt` to S3, confirms it, downloads it and compares the bytes, then deletes it if the token is an Admin's. This writes a document to `SMOKE_PROJECT_ID`, so it is opt-in. If it fails at the PUT with a CORS error from a browser, check the bucket's CORS rules.
- **Against local dev** (`dotnet run --project src/PDS.Api --urls http://localhost:5090`, plus `npm run dev` for the SPA): `scripts/smoke-check.sh http://localhost:5173 http://localhost:5090`. Dev auth accepts every request, so the "401 without a token" check is reported as SKIP. Without the SPA running, the site checks fail; that is expected.

Manual checks the script can't do:

1. Open the `SiteUrl`, sign in with the temporary password from the invitation email, and create a project.
2. Download a Reporting CSV in the browser and open it in Excel: accents and macrons should display correctly and the columns should split.
3. In the browser, upload a file to a project's Documents section, then download it again. This exercises the S3 CORS rules, which the script can't.

### Consent reminder emails

Set `consentReminders` in the deployment file to email a weekday digest of due consent items:

```json
"consentReminders": { "enabled": true, "schedule": "cron(0 7 ? * MON-FRI *)", "timeZone": null, "emails": ["consents@example.com"] }
```

- `enabled`: when absent or `false`, nothing is created.
- `schedule`: an EventBridge Scheduler `cron(...)` or `rate(...)` expression. Default `cron(0 7 ? * MON-FRI *)`.
- `timeZone`: IANA name. Default is `locale.timeZone`.
- `emails`: recipients, subscribed to an SNS topic named `<name>-consent-reminders`. Each recipient must click the confirmation link in the AWS subscription email before they receive digests.

- "Today" in the digest always uses `locale.timeZone` (Locale:TimeZone), even when `consentReminders.timeZone` differs.
- Every email carries SNS's unsubscribe link. A redeploy does not recreate a subscription removed this way, so re-subscribe the address in the SNS console or with `aws sns subscribe --topic-arn <topic-arn> --protocol email --notification-endpoint <address>`; the recipient must confirm again.

A Scheduler rule invokes the API function directly (as a system Admin principal) on `POST /api/consents/reminders/send`. Admins can see the digest content beforehand with `POST /api/consents/reminders/preview`, and trigger a send on demand with `POST /api/consents/reminders/send`. A failed send logs `ConsentReminderSendFailed`, which raises an alarm sent to `alarmEmail`. After the first deploy you can confirm the filter with "Test pattern" in the CloudWatch console.

### Adding a user

Create the user in the Cognito console (or with `admin-create-user`) and add them to exactly one of the groups `Viewer`, `Manager` or `Admin`. A signed-in user with no group sees "No access yet".
