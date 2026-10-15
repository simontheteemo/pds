# PDS Consent Reminders Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A weekday email digest of overdue and due-soon consent items. EventBridge Scheduler invokes the existing API Lambda, which publishes to SNS email. There is also an Admin preview endpoint and button.

**Architecture:**
- The cross-project due-list logic moves into an internal `DueItemsQuery`, shared by `GET /api/consents/due`, `POST /api/consents/reminders/preview` and `POST /api/consents/reminders/send`.
- The schedule's target input is a hand-built API Gateway HTTP API v2 event, so the unchanged host routes it to `/send`, authenticated as a system Admin principal.
- Infra adds one construct (topic, subscriptions, schedule, failure alarm), created only when the deployment enables `consentReminders`.

**Tech Stack:** Same as Consents v1, plus `AWSSDK.SimpleNotificationService` 4.0.100.15 (depends on AWSSDK.Core ≥ 4.0.102.6, the same as the S3 package already used) and the CDK `Amazon.CDK.AWS.Scheduler` / `Amazon.CDK.AWS.Scheduler.Targets` L2 constructs already in `Amazon.CDK.Lib` 2.271.0.

**Spec:** `docs/superpowers/specs/2026-10-14-consent-reminders-design.md`

## Global Constraints

- net10.0, central package versions (`Directory.Packages.props`), `TreatWarningsAsErrors`. The only new package is `AWSSDK.SimpleNotificationService` 4.0.100.15.
- Module rules: only `Contracts/*` and `ConsentsModule` are public in `PDS.Consents`. Everything new is `internal`, except the `ConsentsModule.ReminderSendPath` const.
- **No new tests** (user directive). Verify with:
  - `dotnet format PDS.slnx` then `dotnet build PDS.slnx` (0 warnings)
  - `npm run gen:api && npm run typecheck && npm run lint && npm run build` in `web/`
  - `npx --yes aws-cdk@2.1143.0 synth -c deployment=example-dev --quiet` in `infra/`. This needs `dotnet publish src/PDS.Api -c Release -r linux-arm64 --self-contained false -o artifacts/api` and `web/dist` to exist first.
  - the manual checks each task names
- Run the API on **port 5091**: `ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/PDS.Api --urls http://localhost:5091`. The dev user is Admin. If you run Vite, use port 5201.
- DynamoDB Local is shared on :8000. Reuse it if `curl -s localhost:8000` answers. Never stop it.
- **Shared files get minimal, additive edits only:**
  - `Directory.Packages.props`: one line
  - `infra/PDS.Infra/AppStack.cs`: hoist the log group into a variable, plus one construct line
  - `infra/PDS.Infra/DeploymentSettings.cs`: one new optional parameter, a record and validation
  - `README.md`: one short section

  Don't touch `PDS.Api/Program.cs`, `PDS.slnx`, `PDS.Shared` or `web/src/shared` (other than the regenerated `openapi.json`/`schema.d.ts`).
- Policies: preview and send require `Policies.CanAdminister`. `GET /api/consents/due` stays CanRead and unchanged in behaviour.
- "Today" is `LocalDate.Today(TimeProvider, LocaleOptions)`. Rules stay in `ConsentRules`; don't copy the windows anywhere.
- Commits are conventional (`feat(consents): ...`) and end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **Nothing due → nothing sent.** With zero due items, `/send` returns `NothingDue` and never calls SNS; `/preview` returns null subject and body. Archived projects and Declined/Withdrawn consents never appear (they come from the shared query).
2. **Not configured → never throws.** With no `ConsentReminders:TopicArn` (local dev, or reminders disabled), `/send` returns `NotConfigured` without constructing or calling the SNS client. App startup and build-time OpenAPI generation must not need AWS credentials or a region.
3. **SNS limits.** The subject is ≤ 100 chars, ASCII, with no line breaks. Long or multi-line condition descriptions are collapsed to one line and truncated to 200 chars. More than 100 items are summarised as "…and N more". The body stays well under 256 KB.
4. **Scheduled payload fidelity.** The synthesized schedule input must deserialize as `APIGatewayHttpApiV2ProxyRequest` and reach `POST /api/consents/reminders/send` as an authenticated Admin in Gateway mode. A bad path, method, stage or claims shape returns a silent 401/404 that nobody sees. Verify end to end with the fake Runtime API (Task 3).
5. **Failure visibility.** Any exception in send logs EventId name `ConsentReminderSendFailed` at Error before it propagates, and the metric filter pattern matches Serilog's compact JSON (`"EventId":{"Id":…,"Name":"ConsentReminderSendFailed"}`).

---

### Task 1: Shared due query, digest formatter, preview endpoint (backend)

**Files:**
- Create: `src/Modules/PDS.Consents/Features/DueItemsQuery.cs`
- Modify: `src/Modules/PDS.Consents/Features/ListDue.cs`
- Create: `src/Modules/PDS.Consents/Domain/ConsentLabels.cs`
- Create: `src/Modules/PDS.Consents/Reminders/ConsentDigest.cs`
- Create: `src/Modules/PDS.Consents/Reminders/ConsentRemindersOptions.cs`
- Create: `src/Modules/PDS.Consents/Reminders/ReminderDtos.cs`
- Create: `src/Modules/PDS.Consents/Reminders/PreviewReminders.cs`
- Modify: `src/Modules/PDS.Consents/ConsentsModule.cs`
- Regenerated: `web/src/shared/api/openapi.json` (by the Debug build)

**Interfaces:**
- Produces:
  - `internal sealed class DueItemsQuery { Task<DueItemsResult> LoadAsync(CancellationToken ct); }`
  - `internal sealed record DueItemsResult(DateOnly Today, List<DueItem> Items);`
  - `internal static class ConsentDigest { Digest? Build(DateOnly today, IReadOnlyList<DueItem> items, CultureInfo culture, string? appUrl); }`. It returns null when `items` is empty.
  - `internal sealed record Digest(string Subject, string Body, int ItemCount, int OverdueCount, int DueSoonCount);`
  - `internal sealed class ConsentRemindersOptions { const string Section = "ConsentReminders"; string? TopicArn; string? AppUrl; bool IsConfigured; }`
  - `internal sealed record ReminderPreview(DateOnly Today, int ItemCount, int OverdueCount, int DueSoonCount, string? Subject, string? Body, bool DeliveryConfigured);`
  - Route group `/api/consents/reminders` (CanAdminister), held in `ConsentsModule.MapConsentsEndpoints` as variable `reminders`.

- [ ] **Step 1: Extract `DueItemsQuery`.** Move the body of `ListDue.Handle` into the query unchanged (same filters, same ordering):

```csharp
using Microsoft.Extensions.Options;
using PDS.Consents.Data;
using PDS.Consents.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared.Settings;

namespace PDS.Consents.Features;

internal sealed record DueItemsResult(DateOnly Today, List<DueItem> Items);

/// <summary>
/// The cross-project due list: overdue/due-soon conditions and expiries of active consents on non-archived
/// projects. Shared by GET /api/consents/due and the reminder digest so the rules live in one place.
/// </summary>
internal sealed class DueItemsQuery(
    ConsentStore store, IPortfolioQueries portfolio, TimeProvider clock, IOptions<LocaleOptions> locale)
{
    public async Task<DueItemsResult> LoadAsync(CancellationToken ct)
    {
        var today = LocalDate.Today(clock, locale.Value);
        // ... the existing loop from ListDue.Handle, verbatim ...
        return new DueItemsResult(today, items
            .OrderBy(i => i.Date)
            .ThenBy(i => i.ProjectCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.ConsentTitle, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }
}
```

Then make `ListDue.Handle(DueItemsQuery query, CancellationToken ct) => TypedResults.Ok((await query.LoadAsync(ct)).Items)`, keeping the route `/due` and the name `ListDueConsentItems`. Register it with `services.AddScoped<DueItemsQuery>()` in `AddConsentsModule`.

- [ ] **Step 2: Labels.** `Domain/ConsentLabels.cs`:

```csharp
namespace PDS.Consents.Domain;

/// <summary>Display labels for emails; keep in step with web/src/features/consents/types.ts.</summary>
internal static class ConsentLabels
{
    public static string Type(ConsentType type) => type switch
    {
        ConsentType.ResourceConsent => "Resource consent",
        ConsentType.SubdivisionConsent => "Subdivision consent",
        ConsentType.BuildingConsent => "Building consent",
        ConsentType.Section224c => "s224(c) certificate",
        ConsentType.CodeComplianceCertificate => "Code compliance certificate (CCC)",
        _ => "Other",
    };
}
```

- [ ] **Step 3: Options.** `Reminders/ConsentRemindersOptions.cs`:

```csharp
namespace PDS.Consents.Reminders;

/// <summary>Set by CDK when the deployment enables consent reminders; absent locally.</summary>
internal sealed class ConsentRemindersOptions
{
    public const string Section = "ConsentReminders";

    /// <summary>SNS topic the digest is published to. Unset means reminders are not configured.</summary>
    public string? TopicArn { get; set; }

    /// <summary>Site base URL, used for the "open the register" link.</summary>
    public string? AppUrl { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(TopicArn);
}
```

Register it in `AddConsentsModule` with `services.AddOptions<ConsentRemindersOptions>().BindConfiguration(ConsentRemindersOptions.Section);`.

- [ ] **Step 4: Formatter.** `Reminders/ConsentDigest.cs`. It's pure, with no I/O. The format follows spec §3:

```csharp
using System.Globalization;
using System.Text;
using PDS.Consents.Domain;
using PDS.Consents.Features;

namespace PDS.Consents.Reminders;

internal sealed record Digest(string Subject, string Body, int ItemCount, int OverdueCount, int DueSoonCount);

/// <summary>Plain-text email digest of the due list (SNS email: subject ≤ 100 ASCII chars, body ≤ 256 KB).</summary>
internal static class ConsentDigest
{
    public const int MaxItems = 100;
    public const int MaxDescriptionLength = 200;

    public static Digest? Build(DateOnly today, IReadOnlyList<DueItem> items, CultureInfo culture, string? appUrl)
    {
        if (items.Count == 0)
            return null;

        var overdue = items.Where(i => i.State == DueState.Overdue).ToList();
        var dueSoon = items.Where(i => i.State == DueState.DueSoon).ToList();
        var counts = string.Join(", ", new[]
        {
            overdue.Count > 0 ? $"{overdue.Count} overdue" : null,
            dueSoon.Count > 0 ? $"{dueSoon.Count} due soon" : null,
        }.OfType<string>());

        var body = new StringBuilder();
        body.Append("Consents due - ").AppendLine(today.ToString("dddd d MMMM yyyy", culture)).AppendLine();
        body.Append(counts).AppendLine(".");
        body.Append("(Conditions due within ").Append(ConsentRules.ConditionWarningDays)
            .Append(" days; consent expiries within ").Append(ConsentRules.ExpiryWarningDays).AppendLine(" days.)");

        var remaining = MaxItems;
        AppendSection(body, "OVERDUE", overdue, culture, ref remaining);
        AppendSection(body, "DUE SOON", dueSoon, culture, ref remaining);
        if (items.Count > MaxItems)
            body.AppendLine().Append("...and ").Append(items.Count - MaxItems).AppendLine(" more. Open the register to see them all.");

        if (!string.IsNullOrWhiteSpace(appUrl))
            body.AppendLine().Append("Open the consents register: ").Append(appUrl.TrimEnd('/')).AppendLine("/consents");

        return new Digest($"Consents due: {counts}", body.ToString(), items.Count, overdue.Count, dueSoon.Count);
    }

    private static void AppendSection(
        StringBuilder body, string heading, List<DueItem> items, CultureInfo culture, ref int remaining)
    {
        if (items.Count == 0 || remaining <= 0)
            return;
        body.AppendLine().AppendLine(heading);
        foreach (var item in items.Take(remaining))
        {
            body.Append("- ").Append(item.Date.ToString("d MMM yyyy", culture))
                .Append(" · ").Append(item.ProjectCode).Append(' ').AppendLine(OneLine(item.ProjectName));
            body.Append("  ").Append(ConsentLabels.Type(item.ConsentType)).Append(": ").Append(OneLine(item.ConsentTitle));
            if (item.ConsentReference is { } reference)
                body.Append(" (").Append(OneLine(reference)).Append(')');
            body.AppendLine();
            body.Append("  ");
            if (item.Kind == DueItemKind.Expiry)
                body.AppendLine(item.State == DueState.Overdue ? "Consent expired" : "Consent expires");
            else
            {
                if (item.ConditionReference is { } condRef)
                    body.Append(OneLine(condRef)).Append(": ");
                body.AppendLine(OneLine(item.Description));
            }
        }
        remaining -= Math.Min(items.Count, remaining);
    }

    /// <summary>Collapses whitespace (including line breaks) and truncates to the description limit.</summary>
    private static string OneLine(string value)
    {
        var collapsed = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length <= MaxDescriptionLength ? collapsed : collapsed[..(MaxDescriptionLength - 1)] + "…";
    }
}
```

The subject is always pure ASCII digits and words, so it stays under 100 characters. The heading uses an ASCII hyphen, so the subject never needs one.

- [ ] **Step 5: DTOs and preview endpoint.** `Reminders/ReminderDtos.cs`:

```csharp
namespace PDS.Consents.Reminders;

internal sealed record ReminderPreview(
    DateOnly Today, int ItemCount, int OverdueCount, int DueSoonCount, string? Subject, string? Body, bool DeliveryConfigured);
```

`Reminders/PreviewReminders.cs`:

```csharp
internal static class PreviewReminders
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/preview", Handle).WithName("PreviewConsentReminders");

    internal static async Task<Ok<ReminderPreview>> Handle(
        DueItemsQuery query, IOptions<LocaleOptions> locale, IOptions<ConsentRemindersOptions> options, CancellationToken ct)
    {
        var (today, items) = await query.LoadAsync(ct);
        var digest = ConsentDigest.Build(today, items, CultureInfo.GetCultureInfo(locale.Value.Culture), options.Value.AppUrl);
        return TypedResults.Ok(new ReminderPreview(
            today, items.Count,
            digest?.OverdueCount ?? 0, digest?.DueSoonCount ?? 0,
            digest?.Subject, digest?.Body, options.Value.IsConfigured));
    }
}
```

In `MapConsentsEndpoints`, add:

```csharp
var reminders = app.MapGroup("/api/consents/reminders")
    .WithTags("Consents")
    .RequireAuthorization(Policies.CanAdminister);
PreviewReminders.Map(reminders);
```

- [ ] **Step 6: Verify.** Run `dotnet format PDS.slnx && dotnet build PDS.slnx` (0 warnings; `openapi.json` regenerates). Run the API on 5091 and seed a project (POST `/api/portfolio/projects`; check the route in `PortfolioModule`), a Granted consent expiring in 10 days, a condition due yesterday, a condition with a multi-line description due in 5 days, and a completed condition. Then confirm:
  - `curl -s -X POST localhost:5091/api/consents/reminders/preview` shows subject `Consents due: 1 overdue, 2 due soon`, the body in date order with descriptions on one line, and `deliveryConfigured: false`
  - `GET /api/consents/due` returns the same 3 items as before

- [ ] **Step 7: Commit** `feat(consents): shared due query, reminder digest and preview endpoint`.

---

### Task 2: SNS sender and send endpoint (backend)

**Files:**
- Modify: `Directory.Packages.props`: add `<PackageVersion Include="AWSSDK.SimpleNotificationService" Version="4.0.100.15" />` (keep it alphabetical)
- Modify: `src/Modules/PDS.Consents/PDS.Consents.csproj`: add `<PackageReference Include="AWSSDK.SimpleNotificationService" />`
- Create: `src/Modules/PDS.Consents/Reminders/IReminderSender.cs`
- Create: `src/Modules/PDS.Consents/Reminders/SnsReminderSender.cs`
- Create: `src/Modules/PDS.Consents/Reminders/SendReminders.cs`
- Modify: `src/Modules/PDS.Consents/Reminders/ReminderDtos.cs`
- Modify: `src/Modules/PDS.Consents/ConsentsModule.cs`

**Interfaces:**
- Consumes: `DueItemsQuery`, `ConsentDigest.Build`, `ConsentRemindersOptions`, the `reminders` group (Task 1).
- Produces:
  - `public const string ReminderSendPath = "/api/consents/reminders/send";` on `ConsentsModule`. Infra uses it in Task 3.
  - EventId `new EventId(4101, "ConsentReminderSendFailed")`. Task 3's metric filter matches on its name.
  - `internal enum ReminderSendOutcome { Sent, NothingDue, NotConfigured }`
  - `internal sealed record ReminderSendResult(ReminderSendOutcome Outcome, int ItemCount, string? MessageId);`

- [ ] **Step 1: Package.** Add the version and reference shown above, then run `dotnet restore PDS.slnx`.

- [ ] **Step 2: Sender.**

```csharp
// IReminderSender.cs
namespace PDS.Consents.Reminders;

internal interface IReminderSender
{
    /// <summary>Publishes the digest; returns the provider's message id.</summary>
    Task<string> PublishAsync(string subject, string body, CancellationToken ct);
}

// SnsReminderSender.cs
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Options;

internal sealed class SnsReminderSender(IAmazonSimpleNotificationService sns, IOptions<ConsentRemindersOptions> options)
    : IReminderSender
{
    public async Task<string> PublishAsync(string subject, string body, CancellationToken ct)
    {
        var response = await sns.PublishAsync(
            new PublishRequest { TopicArn = options.Value.TopicArn, Subject = subject, Message = body }, ct);
        return response.MessageId;
    }
}
```

Registration in `AddConsentsModule` uses the same lazy and fallback-region pattern as Documents' S3 client:

```csharp
// Created lazily: only /send with a configured topic resolves it, so local dev and OpenAPI generation never do.
services.AddSingleton<IAmazonSimpleNotificationService>(_ =>
{
    try
    {
        return new AmazonSimpleNotificationServiceClient();
    }
    catch (AmazonClientException)
    {
        return new AmazonSimpleNotificationServiceClient(RegionEndpoint.APSoutheast2);
    }
});
services.AddSingleton<IReminderSender, SnsReminderSender>();
```

- [ ] **Step 3: Send endpoint.** Add `ReminderSendOutcome` and `ReminderSendResult` to `ReminderDtos.cs`. Then `Reminders/SendReminders.cs`:

```csharp
internal static partial class SendReminders
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/send", Handle).WithName("SendConsentReminders");

    internal static async Task<Ok<ReminderSendResult>> Handle(
        IServiceProvider services, DueItemsQuery query, IOptions<LocaleOptions> locale,
        IOptions<ConsentRemindersOptions> options, ILoggerFactory loggers, CancellationToken ct)
    {
        var log = loggers.CreateLogger("PDS.Consents.Reminders");
        try
        {
            if (!options.Value.IsConfigured)
                return Done(log, new ReminderSendResult(ReminderSendOutcome.NotConfigured, 0, null));

            var (today, items) = await query.LoadAsync(ct);
            var digest = ConsentDigest.Build(today, items, CultureInfo.GetCultureInfo(locale.Value.Culture), options.Value.AppUrl);
            if (digest is null)
                return Done(log, new ReminderSendResult(ReminderSendOutcome.NothingDue, 0, null));

            // Resolved here, not injected, so the SNS client is never built unless we actually send.
            var sender = services.GetRequiredService<IReminderSender>();
            var messageId = await sender.PublishAsync(digest.Subject, digest.Body, ct);
            return Done(log, new ReminderSendResult(ReminderSendOutcome.Sent, digest.ItemCount, messageId));
        }
        catch (Exception ex)
        {
            SendFailed(log, ex);
            throw;
        }
    }

    private static Ok<ReminderSendResult> Done(ILogger log, ReminderSendResult result)
    {
        Completed(log, result.Outcome, result.ItemCount);
        return TypedResults.Ok(result);
    }

    [LoggerMessage(EventId = 4100, EventName = "ConsentReminderSendCompleted", Level = LogLevel.Information,
        Message = "Consent reminder digest: {Outcome} ({ItemCount} items)")]
    private static partial void Completed(ILogger log, ReminderSendOutcome outcome, int itemCount);

    /// <summary>The CDK metric filter alarms on this event name; don't rename it.</summary>
    [LoggerMessage(EventId = 4101, EventName = "ConsentReminderSendFailed", Level = LogLevel.Error,
        Message = "Consent reminder digest failed to send")]
    private static partial void SendFailed(ILogger log, Exception ex);
}
```

Add `public const string ReminderSendPath = "/api/consents/reminders/send";` to `ConsentsModule`, with an XML doc noting that the CDK schedule targets it. Then map `SendReminders.Map(reminders);`.

- [ ] **Step 4: Verify.** Run format and build (0 warnings). On 5091, with seeded data from Task 1 (or reseed), check:
  - `POST /api/consents/reminders/send` → `{ "outcome": "NotConfigured", "itemCount": 0 }`
  - with `ConsentReminders__TopicArn=arn:aws:sns:ap-southeast-2:000000000000:nope` set and no AWS credentials → 500 ProblemDetails, and the console log shows `Consent reminder digest failed to send`. Then restart without it.
  - with the TopicArn set but every due item completed → `NothingDue` (no SNS call)
  - temporarily set the dev user's roles to `["Viewer"]` via env `Auth__DevUser__Roles__0=Viewer` → 403 on both reminder routes

- [ ] **Step 5: Commit** `feat(consents): publish reminder digest to SNS via send endpoint`.

---

### Task 3: Infra — deployment settings, schedule, topic, failure alarm, docs

**Files:**
- Modify: `infra/PDS.Infra/DeploymentSettings.cs`
- Create: `infra/PDS.Infra/ConsentReminders.cs`
- Modify: `infra/PDS.Infra/AppStack.cs` (hoist the log group; one construct line)
- Modify: `infra/deployments/example-dev.json`
- Modify: `README.md` (a short "Consent reminder emails" subsection under Deploying)

**Interfaces:**
- Consumes: `ConsentsModule.ReminderSendPath`, the EventId name `ConsentReminderSendFailed`, and the config keys `ConsentReminders__TopicArn` and `ConsentReminders__AppUrl`.

- [ ] **Step 1: Settings.**

```csharp
/// <summary>Weekday email digest of due consent items. Absent or disabled: nothing is created.</summary>
public sealed record ConsentReminderSettings(bool Enabled, string? Schedule, string? TimeZone, string[]? Emails)
{
    public const string DefaultSchedule = "cron(0 7 ? * MON-FRI *)";
}
```

Append `ConsentReminderSettings? ConsentReminders = null` as the last positional parameter of `DeploymentSettings`. In `Load`, when `settings.ConsentReminders is { Enabled: true } r`, throw `InvalidOperationException` in either of these cases:
- any email is blank or has no `@`
- `r.Schedule` is set and doesn't start with `cron(` or `rate(`

- [ ] **Step 2: Construct.** `infra/PDS.Infra/ConsentReminders.cs`:

```csharp
using Amazon.CDK;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.CloudWatch.Actions;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.Scheduler;
using Amazon.CDK.AWS.Scheduler.Targets;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Constructs;
using PDS.Consents;
using LambdaFunction = Amazon.CDK.AWS.Lambda.Function;

namespace PDS.Infra;

/// <summary>
/// Weekday consent digest: EventBridge Scheduler invokes the API function with an HTTP API v2-shaped event for
/// POST <see cref="ConsentsModule.ReminderSendPath"/>, which publishes to an SNS email topic. The event carries a
/// fixed system principal in the authorizer claims; only principals that can invoke the function can send one.
/// </summary>
public sealed class ConsentReminders : Construct
{
    public ConsentReminders(
        Construct scope, string id, DeploymentSettings settings, ConsentReminderSettings reminders,
        LambdaFunction api, ILogGroup apiLogs, ITopic alarms, string siteUrl)
        : base(scope, id)
    {
        var topic = new Topic(this, "topic", new TopicProps
        {
            TopicName = $"{settings.Name}-consent-reminders",
            DisplayName = Truncate(settings.Branding.ProductName, 100),
        });
        foreach (var email in reminders.Emails ?? [])
            topic.AddSubscription(new EmailSubscription(email));
        topic.GrantPublish(api);
        api.AddEnvironment("ConsentReminders__TopicArn", topic.TopicArn);
        api.AddEnvironment("ConsentReminders__AppUrl", siteUrl);

        var path = ConsentsModule.ReminderSendPath;
        var input = new Dictionary<string, object>
        {
            ["version"] = "2.0",
            ["routeKey"] = $"POST {path}",
            ["rawPath"] = path,
            ["rawQueryString"] = "",
            ["headers"] = new Dictionary<string, object>
            {
                ["content-type"] = "application/json",
                ["user-agent"] = "EventBridge Scheduler",
                ["x-correlation-id"] = ContextAttribute.ExecutionId.ToString(),
            },
            ["requestContext"] = new Dictionary<string, object>
            {
                ["routeKey"] = $"POST {path}",
                ["stage"] = "$default",
                ["requestId"] = ContextAttribute.ExecutionId.ToString(),
                ["http"] = new Dictionary<string, object>
                {
                    ["method"] = "POST",
                    ["path"] = path,
                    ["protocol"] = "HTTP/1.1",
                    ["sourceIp"] = "127.0.0.1",
                    ["userAgent"] = "EventBridge Scheduler",
                },
                ["authorizer"] = new Dictionary<string, object>
                {
                    ["jwt"] = new Dictionary<string, object>
                    {
                        ["claims"] = new Dictionary<string, object>
                        {
                            ["sub"] = "system:consent-reminders",
                            ["name"] = "Scheduled consent reminders",
                            ["cognito:groups"] = "[Admin]",
                        },
                        ["scopes"] = Array.Empty<string>(),
                    },
                },
            },
            ["isBase64Encoded"] = false,
        };

        _ = new Schedule(this, "schedule", new ScheduleProps
        {
            Description = "Weekday consent reminder digest",
            Schedule = ScheduleExpression.Expression(
                reminders.Schedule ?? ConsentReminderSettings.DefaultSchedule,
                Amazon.CDK.TimeZone.Of(reminders.TimeZone ?? settings.Locale.TimeZone)),
            Target = new LambdaInvoke(api, new ScheduleTargetBaseProps
            {
                Input = ScheduleTargetInput.FromObject(input),
                RetryAttempts = 2,
                MaxEventAge = Duration.Hours(1),
            }),
        });

        var failures = new MetricFilter(this, "failures", new MetricFilterProps
        {
            LogGroup = apiLogs,
            FilterPattern = FilterPattern.StringValue("$.EventId.Name", "=", "ConsentReminderSendFailed"),
            MetricNamespace = $"PDS/{settings.Name}",
            MetricName = "ConsentReminderFailures",
            MetricValue = "1",
        });
        new Alarm(this, "failed", new AlarmProps
        {
            Metric = failures.Metric(new MetricOptions { Period = Duration.Minutes(5), Statistic = "Sum" }),
            Threshold = 1,
            EvaluationPeriods = 1,
            ComparisonOperator = ComparisonOperator.GREATER_THAN_OR_EQUAL_TO_THRESHOLD,
            TreatMissingData = TreatMissingData.NOT_BREACHING,
            AlarmDescription = "The consent reminder digest failed to send.",
        }).AddAlarmAction(new SnsAction(alarms));
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
```

Adjust to the exact CDK .NET API names if any differ: check `Amazon.CDK.Lib.xml` in `~/.nuget/packages/amazon.cdk.lib/2.271.0/lib/*/`. For example, `ContextAttribute.ExecutionId` may be a static property that returns a `ContextAttribute` whose `ToString()` gives the `<aws.scheduler.execution-id>` token. If the `x-correlation-id` token doesn't render as a plain string, drop that header; the middleware generates an id.

- [ ] **Step 3: AppStack.** Hoist the inline log group (`var apiLogs = new LogGroup(this, "api-logs", ...)` above the function, then `LogGroup = apiLogs`). After `var notify = new SnsAction(alarms);` add:

```csharp
if (settings.ConsentReminders is { Enabled: true } reminders)
    _ = new ConsentReminders(this, "consent-reminders", settings, reminders, api, apiLogs, alarms, siteUrl);
```

- [ ] **Step 4: Example deployment.** Add this to `infra/deployments/example-dev.json`:

```json
"consentReminders": { "enabled": true, "schedule": "cron(0 7 ? * MON-FRI *)", "timeZone": null, "emails": ["consents@example.com"] }
```

- [ ] **Step 5: README.** Add a short subsection under Deploying covering:
  - the `consentReminders` fields and their defaults
  - that each recipient must confirm the AWS subscription email
  - the preview and send endpoints
  - the failure alarm going to `alarmEmail`

  Also add `consentReminders` to the deployment-file field list in step 2.

- [ ] **Step 6: Verify synth.** Publish the API and build the web per Global Constraints, then run `npx --yes aws-cdk@2.1143.0 synth -c deployment=example-dev --quiet` in `infra/`. In `cdk.out/example-dev-app.template.json`, confirm:
  - `AWS::SNS::Topic` with name `example-dev-consent-reminders`, and one email `AWS::SNS::Subscription`
  - `AWS::Scheduler::Schedule` with `ScheduleExpressionTimezone: Pacific/Auckland`, the cron, `RetryPolicy`, and `Target.Input` containing `"rawPath":"/api/consents/reminders/send"`
  - the API function env has `ConsentReminders__TopicArn` and `ConsentReminders__AppUrl`
  - an `sns:Publish` statement in the function's policy
  - an `AWS::Logs::MetricFilter` with pattern `{ $.EventId.Name = "ConsentReminderSendFailed" }`, plus its alarm

  Then copy the deployment file to a scratch name with `"enabled": false` (in `infra/deployments/`, deleted afterwards and never committed) and confirm none of the above appears.

- [ ] **Step 7: End-to-end payload check (Review Focus 4).** In the scratchpad (not committed), write a minimal fake Lambda Runtime API in Python (`http.server`) that:
  - on `GET /2018-06-01/runtime/invocation/next`, serves the schedule's `Target.Input` string from the synthesized template once, with the `<aws.scheduler.execution-id>` token replaced by a UUID. Headers: `Lambda-Runtime-Aws-Request-Id`, `Lambda-Runtime-Deadline-Ms` (now + 30 s), `Lambda-Runtime-Invoked-Function-Arn`.
  - on `POST /2018-06-01/runtime/invocation/<id>/response` (or `/error`), prints the body and exits.

  Run the Debug-built API with these settings:
  - `AWS_LAMBDA_FUNCTION_NAME=pds-test`, `AWS_LAMBDA_RUNTIME_API=127.0.0.1:<port>`
  - `ASPNETCORE_ENVIRONMENT=Development` (so DynamoDB Local is used), with `Auth__Mode=Gateway`
  - `AWS_REGION=ap-southeast-2`

  Expect a response with `statusCode: 200` whose body is `{"outcome":"NotConfigured",...}`. Then rerun with `ConsentReminders__TopicArn` set to a dummy ARN, with no due items (`NothingDue`), or with items (500, plus the `ConsentReminderSendFailed` log line, since there are no real credentials). A 401 or 404 means the payload shape is wrong: fix the construct.

- [ ] **Step 8: Commit** `feat(consents): EventBridge schedule, SNS topic and failure alarm for consent reminders`.

---

### Task 4: Admin preview on the Consents due page (web)

**Files:**
- Modify: `web/src/features/consents/api.ts`
- Modify: `web/src/features/consents/types.ts`
- Create: `web/src/features/consents/ReminderPreviewModal.tsx`
- Modify: `web/src/features/consents/ConsentsDuePage.tsx`
- Regenerated: `web/src/shared/api/schema.d.ts` (`npm run gen:api`)

**Interfaces:**
- Consumes: `POST /api/consents/reminders/preview` → `ReminderPreview` (Task 1).

- [ ] **Step 1: Types and hook.**

```ts
// types.ts
type ReminderPreviewPath = paths['/api/consents/reminders/preview'];
export type ReminderPreview = ReminderPreviewPath['post']['responses'][200]['content']['application/json'];

// api.ts
export function usePreviewReminders() {
  const api = useApi();
  return useMutation({
    mutationFn: () => unwrap<ReminderPreview>(api.POST('/api/consents/reminders/preview')),
  });
}
```

Don't wrap `ReminderPreview` in `DeepRequired`: `subject` and `body` are genuinely nullable.

- [ ] **Step 2: Modal.** `ReminderPreviewModal.tsx` takes `{ opened, onClose }`. When opened, it calls `preview.mutate()` (in an effect keyed on `opened`) and renders:
  - `<Loader />` while pending
  - an `Alert color="red"` with the error message on failure
  - with a subject: `<Text fw={600}>{subject}</Text>` and `<Code block>{body}</Code>`
  - otherwise: `<Text>Nothing is due, so no email would be sent.</Text>`
  - a dimmed note: `deliveryConfigured` ? "Reminder emails are sent on this deployment's schedule." : "Reminder emails are not configured for this deployment."

  Use Mantine `Modal` with `size="xl"` and title "Reminder email preview".

- [ ] **Step 3: Page.** In `ConsentsDuePage`, put the title block in a `Group justify="space-between"`. When `usePermissions().canAdminister`, show `<Button variant="default" onClick={open}>Preview reminder email</Button>` and render the modal (`useDisclosure` from `@mantine/hooks`).

- [ ] **Step 4: Verify.** In `web/`, run `npm run gen:api && npm run typecheck && npm run lint && npm run build`. Optionally run Vite on 5201 against the API on 5091 (CLI/env override only) and click the button.

- [ ] **Step 5: Commit** `feat(consents): admin reminder email preview on the consents due page`.

---

## Spec coverage

| Spec section | Task |
|---|---|
| §2.1 runner, auth, route const, retries | 2 (const), 3 |
| §2.2 SNS delivery | 2, 3 |
| §2.3 module code | 1, 2 |
| §2.4 infra, example-dev | 3 |
| §3 digest content | 1 |
| §4 API | 1 (preview), 2 (send) |
| §5 UI | 4 |
| §6 README | 3 |
| §7 verification | 1–4 |
