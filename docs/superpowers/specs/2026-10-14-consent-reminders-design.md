# PDS Consent Reminders (Consents v2) — Design Spec

- Date: 2026-10-14
- Author: @simon (with Claude)
- Status: **Proceeding on the user's "autonomous, verify later" instruction.** Every decision below was made by Claude and is open to review.
- Builds on: [Consents v1 spec](2026-10-14-consents-design.md) (§6 deferred these reminders) · [Foundation spec](2026-09-29-foundation-design.md)

## 1. Purpose

Consents v1 shipped a "Consents due" page, but people have to remember to open it. This adds a **scheduled email digest** of overdue and due-soon consent items, sent on weekday mornings in the deployment time zone to a per-deployment recipient list. It sends nothing when nothing is due.

### Stated vs decided

| Stated (task brief / v1 §6 sketch) | Decided by Claude (review later) |
|---|---|
| Scheduled digest, e.g. weekday mornings in the deployment time zone, via EventBridge Scheduler | One EventBridge Scheduler schedule per deployment. Default expression `cron(0 7 ? * MON-FRI *)`, time zone `consentReminders.timeZone ?? locale.timeZone`. Scheduler handles DST natively |
| Reuse the due-list logic; don't duplicate rules | The body of `ListDue.Handle` moves into an internal `DueItemsQuery` service. `GET /api/consents/due`, the preview and the send all call it. `ConsentRules` is untouched |
| Separate Lambda entry point vs invoke the API Lambda with a scheduled event | **Invoke the existing API Lambda.** The schedule's input is a hand-built API Gateway HTTP API (v2) event for `POST /api/consents/reminders/send`, so the existing `AddAWSLambdaHosting(HttpApi)` adapter routes it like any request. No new function, no new artifact, no CI change, no `Program.cs` change. See §2.1 |
| SNS email vs SES | **SNS email.** A new `<name>-consent-reminders` topic with one email subscription per configured address; plain text. SES needs a verified domain/identity and a sandbox exit per account, for HTML we don't need. See §2.2 |
| Recipients per deployment: `consentReminders: { enabled, schedule, timeZone?, emails[] }` | Exactly that, optional. Missing or `enabled: false` creates nothing. `emails` may be empty (addresses can be subscribed to the topic by hand). `example-dev.json` gets an enabled example |
| No per-user preference UI | None. Recipients are the deployment list only |
| No email when nothing is due | The send endpoint returns `NothingDue` and publishes nothing |
| Local trigger that prints instead of sending, e.g. `POST /api/consents/reminders/preview` (Admin) | `POST /api/consents/reminders/preview` (CanAdminister) returns the digest subject and body without sending. Also an Admin-only "Preview reminder email" button on the Consents due page (trivial: one button + modal) |
| — | `POST /api/consents/reminders/send` (CanAdminister) builds and publishes. It is the scheduler's target and also lets an Admin send on demand. Without a configured topic (local dev, or reminders disabled) it returns `NotConfigured` and sends nothing |
| — | The scheduled event authenticates as a fixed system principal (`sub = system:consent-reminders`, `cognito:groups = [Admin]`) placed in `requestContext.authorizer.jwt.claims`. See §2.1 for why this is safe |
| — | Failures alarm: the send logs a named error event (`ConsentReminderSendFailed`); a CloudWatch Logs metric filter on the API log group (a term match on `ConsentReminderSendFailed`, not a JSON selector, so it works whether or not Lambda prefixes or wraps the JSON line) feeds an alarm on the existing alarms topic. Needed because an HTTP 500 is a *successful* Lambda invocation, so `api-function-errors` would never fire |
| v1 §3.2: "revisit with a sparse due-date GSI when the digest arrives" | **Keep the Scan.** One extra Scan per weekday is negligible at the expected volume (hundreds of consents) |
| v1 §6 sketch: public `IConsentsDigest` in `Contracts/` | Not needed: the digest lives inside the Consents module and is reached over HTTP, so nothing outside the module calls it |
| New package | `AWSSDK.SimpleNotificationService` (official AWS SDK, same family and major version as the DynamoDB and S3 clients already used). The only way to publish to SNS without hand-signing requests |
| No new tests (standing directive) | Verify by format, build, gen:api, typecheck, lint, web build, synth, manual API checks, and a local fake Lambda Runtime API run that feeds the **synthesized** schedule input to the real host in Gateway auth mode (§7) |

### Success criteria
- With `consentReminders.enabled`, `cdk synth` produces an SNS topic with the configured email subscriptions, an EventBridge Scheduler schedule targeting the API function with the configured cron and time zone, a publish grant and env vars on the API function, and the failure metric filter + alarm. With it absent, none of these exist.
- The scheduled input, delivered to the real API host, reaches `POST /api/consents/reminders/send` as the system Admin principal.
- `POST /api/consents/reminders/preview` returns the same items as `GET /api/consents/due`, formatted as the email.
- Nothing is published when nothing is due or when no topic is configured.

## 2. Architecture

### 2.1 How it runs: a scheduled HTTP-shaped event to the API Lambda

```
EventBridge Scheduler (cron, tz) ──Invoke(async, input = HTTP API v2 event)──▶ API Lambda (unchanged host)
     └─ POST /api/consents/reminders/send ──▶ DueItemsQuery ──▶ ConsentDigest ──▶ SNS Publish ──▶ email
```

Options considered:

1. **Separate function** in the same artifact with a class-library handler (`PDS.Api::...::Handle`). It needs its own DI composition (settings, Dynamo, Portfolio, Consents), which would duplicate the host's. It also adds a second function, log group, grants and alarms, and a cold-start path we can't run locally.
2. **Mode switch in `Program.cs`** (an env var picks a different entry point). This touches a shared wiring file that every module branch edits, and risks the API's startup path.
3. **Chosen: invoke the API function with an HTTP API v2-shaped payload.** The Lambda adapter deserializes any input as `APIGatewayHttpApiV2ProxyRequest`; Scheduler lets the target input be arbitrary JSON. The digest is then a normal Minimal API endpoint inside the Consents module, with the normal DI, logging and auth pipeline. The only new infra is the schedule, topic and alarm.

**Auth.** In Gateway mode the host trusts `requestContext.authorizer.jwt.claims`, because only API Gateway (after JWT validation) or an IAM principal with `lambda:InvokeFunction` can deliver an event. The schedule's input carries a fixed system principal with the Admin group. That grants nothing new: anyone who can invoke the function or edit the schedule could already do this. The Scheduler's IAM role is scoped to invoking this one function (CDK's `LambdaInvoke` target creates it).

**Route constant.** `ConsentsModule.ReminderSendPath = "/api/consents/reminders/send"` is public, and infra (which already references `PDS.Consents` for `Tables`) uses it to build the payload, so the two can't drift.

**Retries.** The target uses `RetryAttempts = 2` and `MaxEventAge = 1 hour`. A digest that arrives hours late is noise, and a retry after a partial success could double-send. The function's own async invoke config is set to 0 retries and a 1 hour max age, so Lambda's default async retries (2 retries, 6h) don't undercut this.

### 2.2 Delivery: SNS email

- Topic `<name>-consent-reminders`, `DisplayName` = `branding.productName` (truncated to 100). SNS uses the display name as the email sender name, so recipients see which system it is.
- One `EmailSubscription` per configured address. Each recipient must click AWS's confirmation link once, and every email carries SNS's unsubscribe link. Both are documented in the README.
- The body is plain text. The subject is ≤ 100 ASCII characters (an SNS limit), e.g. `Consents due: 2 overdue, 5 due soon`.
- SES was rejected for v1: it needs a verified sending identity per account, a production-access request to leave the sandbox, and a domain/DNS decision per client, all for HTML formatting nobody asked for. Moving to SES later only changes the `IReminderSender` implementation.

### 2.3 Module code (all in `PDS.Consents`, internal unless noted)

| Unit | Purpose |
|---|---|
| `Features/DueItemsQuery.cs` | `LoadAsync(ct) → (DateOnly Today, List<DueItem> Items)`. The previous `ListDue.Handle` body, unchanged in behaviour. Registered scoped |
| `Features/ListDue.cs` | Calls `DueItemsQuery`; same route, name and response |
| `Reminders/ConsentDigest.cs` | Pure formatter: `Build(today, items, culture, appUrl) → Digest(Subject, Body, ItemCount, OverdueCount, DueSoonCount)`. No I/O |
| `Reminders/ConsentRemindersOptions.cs` | Bound from `ConsentReminders`: `TopicArn?`, `AppUrl?`. `IsConfigured` = TopicArn set |
| `Reminders/IReminderSender.cs` + `SnsReminderSender.cs` | `PublishAsync(subject, body, ct) → messageId`. The SNS client is created lazily (same pattern as Documents' S3 client) |
| `Reminders/PreviewReminders.cs`, `Reminders/SendReminders.cs` | The two endpoints |
| `ConsentsModule` (public) | Adds `ReminderSendPath`, registers the above, maps a `/api/consents/reminders` group with `CanAdminister` |
| `Domain/ConsentLabels.cs` | Server-side display labels for `ConsentType` (same text as the web `consentTypeLabel`), used in the email |

The host is unchanged: `AddConsentsModule()` binds its own options from `IConfiguration` (`BindConfiguration`).

### 2.4 Infra

- `DeploymentSettings` gains an optional last parameter, `ConsentReminderSettings? ConsentReminders`, as `record ConsentReminderSettings(bool Enabled, string? Schedule, string? TimeZone, string[]? Emails)`. Load-time validation: when enabled, every email must contain `@`, and `schedule` (if set) must start with `cron(` or `rate(`.
- A new construct, `infra/PDS.Infra/ConsentReminders.cs` (same style as `DocumentsBucket.cs`), is created from `AppStack` only when enabled. It takes `settings`, the API function, the API log group, the alarms topic and the site URL, and creates:
  - the topic and subscriptions, plus `topic.GrantPublish(api)`
  - the env vars `ConsentReminders__TopicArn` and `ConsentReminders__AppUrl` (the site URL), added via `api.AddEnvironment`
  - `Schedule` (`Amazon.CDK.AWS.Scheduler`) with `ScheduleExpression.Expression(schedule, TimeZone.Of(tz))` and a `LambdaInvoke` target whose input is `ScheduleTargetInput.FromObject(<HTTP API v2 event>)`
  - a `MetricFilter` on the API log group doing a term match on `ConsentReminderSendFailed` (not a JSON selector; it works whether or not Lambda prefixes or wraps the JSON line), metric `PDS/<name>` `ConsentReminderFailures`, and an alarm (≥ 1 in 5 min, missing = not breaching) that notifies the alarms topic
- AppStack edits: hoist the API `LogGroup` into a local variable, and add one `if (...) _ = new ConsentReminders(...)` line after the alarms topic.
- `example-dev.json` gains `"consentReminders": { "enabled": true, "schedule": "cron(0 7 ? * MON-FRI *)", "emails": ["consents@example.com"] }`.

## 3. Digest content

Items are exactly `DueItemsQuery`'s output: the same items and order as the Consents due page.

**Subject:** `Consents due: {n} overdue, {m} due soon`. A zero count is omitted (`Consents due: 3 due soon`).

**Body** (dates in `Locale:Culture`, format `d MMM yyyy`):

```
Consents due — Friday 16 October 2026

2 overdue, 5 due soon.
(Conditions due within 30 days; consent expiries within 90 days.)

OVERDUE
- 12 Oct 2026 · P001 Riverside Apartments
  Resource consent: Land use consent – stage 2 (LUC60012345)
  Condition 14: Submit landscaping plan

DUE SOON
- 20 Oct 2026 · P002 Hilltop
  Building consent: Stage 1 building consent
  Consent expires

Open the consents register: https://<site>/consents
```

- An expiry item reads "Consent expires" (overdue: "Consent expired").
- Descriptions are collapsed to one line and truncated to 200 characters.
- At most 100 items are listed. Beyond that: `…and N more. Open the register to see them all.` This keeps us far under SNS's 256 KB limit.
- The link line appears only when `AppUrl` is configured.

## 4. API

Group `/api/consents/reminders`, tag `Consents`, policy **CanAdminister**.

| Method | Route | Response |
|---|---|---|
| POST | `/preview` | `200 ReminderPreview { today, itemCount, overdueCount, dueSoonCount, subject?, body?, deliveryConfigured }`. `subject`/`body` are null when nothing is due |
| POST | `/send` | `200 ReminderSendResult { outcome: Sent \| NothingDue \| NotConfigured, itemCount, messageId? }` |

Send order: check configuration → load items → nothing due → build → publish. When nothing is due, nothing is sent. On any exception, log `ConsentReminderSendFailed` (Error) and rethrow, which becomes the usual 500 ProblemDetails. Each run logs one Information line with the outcome and counts.

## 5. UI

On `ConsentsDuePage`, Admins (`usePermissions().canAdminister`) see a **"Preview reminder email"** button in the header. It calls the preview endpoint and opens a modal with:
- the subject (bold) and the body in a monospace `<Code block>`
- "Nothing is due, so no email would be sent." when nothing is due
- a dimmed note: "Reminder emails are sent on the deployment's schedule." or "Reminder emails are not configured for this deployment."

There is no send button in the UI. Sending on demand stays an API-only Admin action, to avoid accidental mass emails.

## 6. Operational notes (README)

- To turn reminders on, add `consentReminders` to the deployment file and deploy. Each recipient confirms the AWS subscription email once.
- The schedule time zone defaults to `locale.timeZone`. "Today" in the digest always uses `Locale:TimeZone`.
- To test a deployed environment, an Admin calls `POST /api/consents/reminders/preview`, or `/send` to send now.

## 7. Verification

- `dotnet format`, `dotnet build` (0 warnings), web gen:api, typecheck, lint and build; `cdk synth -c deployment=example-dev`.
- Manual (Development, port 5091): seed a project, a consent and conditions (overdue and due soon). Then:
  - preview returns the expected subject and body
  - send returns `NotConfigured`
  - with no due items, preview returns a null subject
- **End-to-end routing check:** take the schedule's `Input` from the synthesized template and run the API with `AWS_LAMBDA_RUNTIME_API` pointed at a ~40-line local fake Runtime API (scratchpad only, not committed), with `Auth__Mode=Gateway`. Confirm the response is a 200 `NotConfigured`/`NothingDue` result from the send endpoint, which proves the payload deserializes, routes and authenticates as Admin.

## 8. Out of scope
- SES/HTML email, per-user subscriptions or preferences, per-project recipients
- Digest frequency other than the one schedule; snoozing items
- Reminders for other modules
- A due-date GSI
