# PDS Risk Registry (v1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a per-project risk register: a new `PDS.Risks` module (DynamoDB table, API) and a Risks section on the project detail page.

**Architecture:** A new module project `src/Modules/PDS.Risks` mirrors `PDS.Portfolio`: `Contracts/` is public (`RiskId`), everything else is internal, and `RisksModule` handles registration. It reads projects only through `PDS.Portfolio.Contracts.IPortfolioQueries`. It owns the `risks` table (`pk = PROJECT#<projectId>`, `sk = RISK#<riskId>`, no GSI). The web app adds `web/src/features/risks` and renders `ProjectRisksSection` on the project detail page.

**Tech Stack:** Same as the foundation: .NET 10 Minimal APIs, AWSSDK.DynamoDBv2, FluentValidation, React 19 + Mantine 9 + TanStack Query + Zod 4, AWS CDK (C#).

**Spec:** `docs/superpowers/specs/2026-09-30-risk-registry-design.md`. Follow the existing Portfolio code as the reference implementation for patterns.

## Global Constraints

- Everything from the foundation plan's Global Constraints still applies: net10.0, central package versions, `internal` everywhere except Contracts and the Module class, `pk`/`sk` string keys, ProblemDetails, and camelCase JSON with string enums.
- **No new tests** (user directive). Verify with `dotnet format PDS.slnx`, `dotnet build PDS.slnx` (0 warnings), `npm run gen:api && npm run typecheck && npm run lint && npm run build` in web/, `cdk synth`, and the manual checks named in each task.
- Rating rules: likelihood and impact are integers 1–5. Score = L × I. Bands: Low 1–4, Medium 5–9, High 10–16, Extreme 20–25.
- Status: `Open | Mitigating | Closed`. PUT may only set Open or Mitigating. Closing requires a note (≤ 2000 characters). A closed risk can't be edited until it's reopened. Nothing is deleted.
- Policies: reads need CanRead; create, update, close and reopen need CanWrite.
- Commit messages end with the Co-Authored-By trailer your environment specifies.

## Review Focus

1. **Closed-risk edit.** A PUT on a closed risk must return 400 on `status` ("Reopen the risk before editing it."), not silently reopen it or save the change.
2. **Archived project.** POSTing a risk to an archived project returns 400 on `projectId`. Listing its existing risks still works.
3. **Unknown project.** Listing risks, or creating one, for a project id that doesn't exist returns 404, not an empty list.
4. **Stale edits.** Two Managers editing the same risk: the second save gets 409, and the UI refreshes and explains what happened.
5. **Ordering.** Open and Mitigating risks come before Closed, then by score (highest first), then by title, so Extreme risks are always at the top.

---

### Task 1: `PDS.Risks` module, host wiring and infrastructure table

**Files:**
- Create: `src/Modules/PDS.Risks/PDS.Risks.csproj`, `RisksModule.cs`
- Create: `Contracts/RiskId.cs`
- Create: `Domain/AuditStamp.cs`, `Domain/RiskCategory.cs`, `Domain/RiskStatus.cs`, `Domain/RiskBand.cs`, `Domain/RiskFields.cs`, `Domain/RiskRules.cs`, `Domain/Risk.cs`
- Create: `Data/RisksTable.cs`, `Data/RiskItemMapper.cs`, `Data/RiskStore.cs`
- Create: `Features/RiskDtos.cs`, `Features/RiskValidators.cs`, `Features/RisksProblems.cs`, `Features/ProjectLookup.cs`, `Features/ListRisks.cs`, `Features/GetRisk.cs`, `Features/CreateRisk.cs`, `Features/UpdateRisk.cs`, `Features/CloseRisk.cs`
- Modify: `PDS.slnx` (add project), `src/PDS.Api/PDS.Api.csproj` (reference), `src/PDS.Api/Program.cs` (two lines), `infra/PDS.Infra/Program.cs` (table list)
- Generated: `web/src/shared/api/openapi.json` (by the Debug build); `web/src/shared/api/schema.d.ts` (`npm run gen:api`)

**Interfaces:**
- Consumes: `PDS.Portfolio.Contracts.{ProjectId, ProjectSummary, IPortfolioQueries}`; `PDS.Shared.{Data.Attr, Data.TableKeys, Data.TableNames, Data.TableDefinition, Security.Policies, Security.ICurrentUser, Web.Problems, Web.WithValidation<T>}`.
- Produces (public): `RiskId`, `RisksModule.Tables`, `AddRisksModule()`, `MapRisksEndpoints()`.
- Produces (HTTP), under `/api/risks/projects/{projectId}/risks`:
  - `GET ?includeClosed=` → `RiskDetails[]`
  - `GET /{riskId}`
  - `POST` → 201
  - `PUT /{riskId}`
  - `POST /{riskId}/close`
  - `POST /{riskId}/reopen`
- Operation names: `ListRisks`, `GetRisk`, `CreateRisk`, `UpdateRisk`, `CloseRisk`, `ReopenRisk`.

- [ ] **Step 1: Project file and solution**

`src/Modules/PDS.Risks/PDS.Risks.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../PDS.Shared/PDS.Shared.csproj" />
    <!-- Only PDS.Portfolio.Contracts types may be used (module rule). -->
    <ProjectReference Include="../PDS.Portfolio/PDS.Portfolio.csproj" />
  </ItemGroup>
</Project>
```
Run: `dotnet sln PDS.slnx add src/Modules/PDS.Risks/PDS.Risks.csproj`

- [ ] **Step 2: Contracts and domain**

`Contracts/RiskId.cs`:
```csharp
namespace PDS.Risks.Contracts;

public readonly record struct RiskId(Guid Value)
{
    public static RiskId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
```

`Domain/AuditStamp.cs`:
```csharp
namespace PDS.Risks.Domain;

internal readonly record struct AuditStamp(DateTimeOffset At, string By);
```

`Domain/RiskCategory.cs`:
```csharp
namespace PDS.Risks.Domain;

internal enum RiskCategory
{
    Geotechnical,
    Consenting,
    Contractual,
    ContractorDefault,
    Market,
    Financial,
    Design,
    HealthAndSafety,
    Environmental,
    Other,
}
```

`Domain/RiskStatus.cs`:
```csharp
namespace PDS.Risks.Domain;

internal enum RiskStatus
{
    Open,
    Mitigating,
    Closed,
}
```

`Domain/RiskBand.cs`:
```csharp
namespace PDS.Risks.Domain;

internal enum RiskBand
{
    Low,
    Medium,
    High,
    Extreme,
}
```

`Domain/RiskFields.cs`:
```csharp
namespace PDS.Risks.Domain;

/// <summary>Everything a user edits on a risk (status is changed separately).</summary>
internal sealed record RiskFields(
    string Title,
    RiskCategory Category,
    string? Description,
    int Likelihood,
    int Impact,
    string? Mitigation,
    string? Owner,
    DateOnly? DueDate);
```

`Domain/RiskRules.cs`:
```csharp
namespace PDS.Risks.Domain;

internal static class RiskRules
{
    public const int TitleMaxLength = 200;
    public const int TextMaxLength = 4000;
    public const int OwnerMaxLength = 200;
    public const int ClosingNoteMaxLength = 2000;
    public const int MinRating = 1;
    public const int MaxRating = 5;

    public static int Score(int likelihood, int impact) => likelihood * impact;

    public static RiskBand BandFor(int score) => score switch
    {
        <= 4 => RiskBand.Low,
        <= 9 => RiskBand.Medium,
        <= 16 => RiskBand.High,
        _ => RiskBand.Extreme,
    };

    public static bool IsValidRating(int value) => value is >= MinRating and <= MaxRating;

    public static RiskFields Normalise(RiskFields f) => f with
    {
        Title = (f.Title ?? string.Empty).Trim(),
        Description = Clean(f.Description),
        Mitigation = Clean(f.Mitigation),
        Owner = Clean(f.Owner),
    };

    /// <summary>Guards invariants; the API validators report the same rules as field errors first.</summary>
    public static void EnsureValid(RiskFields f)
    {
        if (f.Title.Length == 0)
            throw new ArgumentException("Risk title is required.", nameof(f));
        if (!IsValidRating(f.Likelihood) || !IsValidRating(f.Impact))
            throw new ArgumentException("Likelihood and impact must be between 1 and 5.", nameof(f));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
```

`Domain/Risk.cs`:
```csharp
using PDS.Portfolio.Contracts;
using PDS.Risks.Contracts;

namespace PDS.Risks.Domain;

internal sealed class Risk
{
    private Risk(
        RiskId id, ProjectId projectId, RiskFields fields, RiskStatus status, string? closingNote,
        AuditStamp? closed, AuditStamp created, AuditStamp updated, long version)
    {
        Id = id;
        ProjectId = projectId;
        Fields = fields;
        Status = status;
        ClosingNote = closingNote;
        Closed = closed;
        Created = created;
        Updated = updated;
        Version = version;
    }

    public RiskId Id { get; }

    public ProjectId ProjectId { get; }

    public RiskFields Fields { get; private set; }

    public RiskStatus Status { get; private set; }

    public string? ClosingNote { get; private set; }

    public AuditStamp? Closed { get; private set; }

    public AuditStamp Created { get; }

    public AuditStamp Updated { get; private set; }

    public long Version { get; private set; }

    public int Score => RiskRules.Score(Fields.Likelihood, Fields.Impact);

    public RiskBand Band => RiskRules.BandFor(Score);

    public static Risk Create(ProjectId projectId, RiskFields fields, AuditStamp stamp)
    {
        var normalised = RiskRules.Normalise(fields);
        RiskRules.EnsureValid(normalised);
        return new Risk(RiskId.New(), projectId, normalised, RiskStatus.Open, null, null, stamp, stamp, 1);
    }

    public static Risk Rehydrate(
        RiskId id, ProjectId projectId, RiskFields fields, RiskStatus status, string? closingNote,
        AuditStamp? closed, AuditStamp created, AuditStamp updated, long version) =>
        new(id, projectId, fields, status, closingNote, closed, created, updated, version);

    /// <summary>Edits fields and moves between Open and Mitigating. Closed risks must be reopened first.</summary>
    public void Update(RiskFields fields, RiskStatus status, AuditStamp stamp)
    {
        if (Status == RiskStatus.Closed)
            throw new InvalidOperationException("A closed risk must be reopened before it is edited.");
        if (status == RiskStatus.Closed)
            throw new ArgumentException("Use Close to close a risk.", nameof(status));

        var normalised = RiskRules.Normalise(fields);
        RiskRules.EnsureValid(normalised);
        Fields = normalised;
        Status = status;
        Touch(stamp);
    }

    /// <returns>False when the risk was already closed (nothing changed).</returns>
    public bool Close(string note, AuditStamp stamp)
    {
        if (Status == RiskStatus.Closed)
            return false;
        var trimmed = (note ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new ArgumentException("A closing note is required.", nameof(note));

        Status = RiskStatus.Closed;
        ClosingNote = trimmed;
        Closed = stamp;
        Touch(stamp);
        return true;
    }

    /// <returns>False when the risk was not closed (nothing changed).</returns>
    public bool Reopen(AuditStamp stamp)
    {
        if (Status != RiskStatus.Closed)
            return false;

        Status = RiskStatus.Open;
        ClosingNote = null;
        Closed = null;
        Touch(stamp);
        return true;
    }

    private void Touch(AuditStamp stamp)
    {
        Updated = stamp;
        Version++;
    }
}
```

- [ ] **Step 3: Data layer**

`Data/RisksTable.cs`:
```csharp
using PDS.Shared.Data;

namespace PDS.Risks.Data;

internal static class RisksTable
{
    public const string LogicalName = "risks";

    public static readonly TableDefinition Definition = new(LogicalName, []);
}
```

`Data/RiskItemMapper.cs`:
```csharp
using Amazon.DynamoDBv2.Model;
using PDS.Portfolio.Contracts;
using PDS.Risks.Contracts;
using PDS.Risks.Domain;
using PDS.Shared.Data;

namespace PDS.Risks.Data;

/// <summary>Risk item: pk=PROJECT#projectId, sk=RISK#riskId. Optional attributes are omitted when empty.</summary>
internal static class RiskItemMapper
{
    public const string RiskPrefix = "RISK#";

    public static string PartitionKey(ProjectId projectId) => $"PROJECT#{projectId.Value}";

    public static Dictionary<string, AttributeValue> Key(ProjectId projectId, RiskId riskId) => new()
    {
        [TableKeys.PartitionKey] = Attr.S(PartitionKey(projectId)),
        [TableKeys.SortKey] = Attr.S($"{RiskPrefix}{riskId.Value}"),
    };

    public static Dictionary<string, AttributeValue> ToItem(Risk risk)
    {
        var f = risk.Fields;
        var item = Key(risk.ProjectId, risk.Id);
        item["id"] = Attr.S(risk.Id.Value.ToString());
        item["projectId"] = Attr.S(risk.ProjectId.Value.ToString());
        item["title"] = Attr.S(f.Title);
        item["category"] = Attr.S(f.Category.ToString());
        Attr.SetIfPresent(item, "description", f.Description);
        item["likelihood"] = Attr.N(f.Likelihood);
        item["impact"] = Attr.N(f.Impact);
        Attr.SetIfPresent(item, "mitigation", f.Mitigation);
        Attr.SetIfPresent(item, "owner", f.Owner);
        Attr.SetIfPresent(item, "dueDate", f.DueDate);
        item["status"] = Attr.S(risk.Status.ToString());
        Attr.SetIfPresent(item, "closingNote", risk.ClosingNote);
        if (risk.Closed is { } closed)
        {
            item["closedAt"] = Attr.Timestamp(closed.At);
            item["closedBy"] = Attr.S(closed.By);
        }

        item["createdAt"] = Attr.Timestamp(risk.Created.At);
        item["createdBy"] = Attr.S(risk.Created.By);
        item["updatedAt"] = Attr.Timestamp(risk.Updated.At);
        item["updatedBy"] = Attr.S(risk.Updated.By);
        item["version"] = Attr.N(risk.Version);
        return item;
    }

    public static Risk FromItem(IReadOnlyDictionary<string, AttributeValue> item)
    {
        var fields = new RiskFields(
            Title: Attr.GetString(item, "title"),
            Category: Enum.Parse<RiskCategory>(Attr.GetString(item, "category")),
            Description: Attr.GetStringOrNull(item, "description"),
            Likelihood: (int)Attr.GetLong(item, "likelihood"),
            Impact: (int)Attr.GetLong(item, "impact"),
            Mitigation: Attr.GetStringOrNull(item, "mitigation"),
            Owner: Attr.GetStringOrNull(item, "owner"),
            DueDate: Attr.GetDateOrNull(item, "dueDate"));

        AuditStamp? closed = Attr.GetStringOrNull(item, "closedAt") is not null
            ? new AuditStamp(Attr.GetTimestamp(item, "closedAt"), Attr.GetString(item, "closedBy"))
            : null;

        return Risk.Rehydrate(
            new RiskId(Guid.Parse(Attr.GetString(item, "id"))),
            new ProjectId(Guid.Parse(Attr.GetString(item, "projectId"))),
            fields,
            Enum.Parse<RiskStatus>(Attr.GetString(item, "status")),
            Attr.GetStringOrNull(item, "closingNote"),
            closed,
            new AuditStamp(Attr.GetTimestamp(item, "createdAt"), Attr.GetString(item, "createdBy")),
            new AuditStamp(Attr.GetTimestamp(item, "updatedAt"), Attr.GetString(item, "updatedBy")),
            Attr.GetLong(item, "version"));
    }
}
```

`Data/RiskStore.cs`:
```csharp
using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using PDS.Portfolio.Contracts;
using PDS.Risks.Contracts;
using PDS.Risks.Domain;
using PDS.Shared.Data;

namespace PDS.Risks.Data;

internal sealed class RiskStore(IAmazonDynamoDB dynamo, TableNames names)
{
    private string Table => names.For(RisksTable.LogicalName);

    public async Task<Risk?> GetAsync(ProjectId projectId, RiskId riskId, CancellationToken ct)
    {
        var response = await dynamo.GetItemAsync(
            new GetItemRequest { TableName = Table, Key = RiskItemMapper.Key(projectId, riskId), ConsistentRead = true }, ct);
        return response.Item is { Count: > 0 } item ? RiskItemMapper.FromItem(item) : null;
    }

    public async Task<IReadOnlyList<Risk>> ListForProjectAsync(ProjectId projectId, CancellationToken ct)
    {
        var risks = new List<Risk>();
        Dictionary<string, AttributeValue>? start = null;
        do
        {
            var response = await dynamo.QueryAsync(new QueryRequest
            {
                TableName = Table,
                KeyConditionExpression = "pk = :pk AND begins_with(sk, :prefix)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":pk"] = Attr.S(RiskItemMapper.PartitionKey(projectId)),
                    [":prefix"] = Attr.S(RiskItemMapper.RiskPrefix),
                },
                ConsistentRead = true,
                ExclusiveStartKey = start,
            }, ct);
            risks.AddRange((response.Items ?? []).Select(RiskItemMapper.FromItem));
            start = response.LastEvaluatedKey is { Count: > 0 } next ? next : null;
        }
        while (start is not null);

        return risks;
    }

    public Task CreateAsync(Risk risk, CancellationToken ct) =>
        dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = Table,
            Item = RiskItemMapper.ToItem(risk),
            ConditionExpression = "attribute_not_exists(pk)",
        }, ct);

    /// <returns>False when the stored version no longer equals <paramref name="expectedVersion"/>.</returns>
    public async Task<bool> UpdateAsync(Risk risk, long expectedVersion, CancellationToken ct)
    {
        try
        {
            await dynamo.PutItemAsync(new PutItemRequest
            {
                TableName = Table,
                Item = RiskItemMapper.ToItem(risk),
                ConditionExpression = "#version = :expected",
                ExpressionAttributeNames = new Dictionary<string, string> { ["#version"] = "version" },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":expected"] = new() { N = expectedVersion.ToString(CultureInfo.InvariantCulture) },
                },
            }, ct);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }
}
```

Note: `Attr.N` takes `decimal` or `long`, so an `int` argument binds to `N(long)`. If the compiler reports it as ambiguous, cast to `(long)`.

- [ ] **Step 4: DTOs, validators, problems and project lookup**

`Features/RiskDtos.cs`:
```csharp
using PDS.Risks.Domain;

namespace PDS.Risks.Features;

/// <summary>Editable fields. Category, likelihood and impact are nullable so omission is a 400, not a default.</summary>
internal interface IRiskInput
{
    string Title { get; }

    RiskCategory? Category { get; }

    string? Description { get; }

    int? Likelihood { get; }

    int? Impact { get; }

    string? Mitigation { get; }

    string? Owner { get; }

    DateOnly? DueDate { get; }
}

internal sealed record CreateRiskRequest(
    string Title, RiskCategory? Category, string? Description, int? Likelihood, int? Impact,
    string? Mitigation, string? Owner, DateOnly? DueDate) : IRiskInput;

internal sealed record UpdateRiskRequest(
    string Title, RiskCategory? Category, string? Description, int? Likelihood, int? Impact,
    string? Mitigation, string? Owner, DateOnly? DueDate, RiskStatus? Status, long Version) : IRiskInput;

internal sealed record CloseRiskRequest(long Version, string Note);

internal sealed record ReopenRiskRequest(long Version);

internal static class RiskInputMapping
{
    /// <summary>Call only after validation.</summary>
    public static RiskFields ToFields(this IRiskInput input) => new(
        input.Title, input.Category!.Value, input.Description, input.Likelihood!.Value, input.Impact!.Value,
        input.Mitigation, input.Owner, input.DueDate);
}

internal sealed record RiskDetails(
    Guid Id,
    Guid ProjectId,
    string Title,
    RiskCategory Category,
    string? Description,
    int Likelihood,
    int Impact,
    int Score,
    RiskBand Band,
    string? Mitigation,
    string? Owner,
    DateOnly? DueDate,
    RiskStatus Status,
    string? ClosingNote,
    DateTimeOffset? ClosedAt,
    string? ClosedBy,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    long Version)
{
    public static RiskDetails From(Risk r)
    {
        var f = r.Fields;
        return new RiskDetails(
            r.Id.Value, r.ProjectId.Value, f.Title, f.Category, f.Description, f.Likelihood, f.Impact, r.Score, r.Band,
            f.Mitigation, f.Owner, f.DueDate, r.Status, r.ClosingNote, r.Closed?.At, r.Closed?.By,
            r.Created.At, r.Created.By, r.Updated.At, r.Updated.By, r.Version);
    }
}
```

`Features/RiskValidators.cs`:
```csharp
using FluentValidation;
using PDS.Risks.Domain;

namespace PDS.Risks.Features;

internal abstract class RiskInputValidator<T> : AbstractValidator<T> where T : IRiskInput
{
    protected RiskInputValidator()
    {
        RuleFor(x => x.Title).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(RiskRules.TitleMaxLength);
        RuleFor(x => x.Category).NotNull().WithMessage("Choose a category.").IsInEnum();
        RuleFor(x => x.Likelihood).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Choose a likelihood.")
            .InclusiveBetween(RiskRules.MinRating, RiskRules.MaxRating).WithMessage("Likelihood must be between 1 and 5.");
        RuleFor(x => x.Impact).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Choose an impact.")
            .InclusiveBetween(RiskRules.MinRating, RiskRules.MaxRating).WithMessage("Impact must be between 1 and 5.");
        RuleFor(x => x.Description).MaximumLength(RiskRules.TextMaxLength);
        RuleFor(x => x.Mitigation).MaximumLength(RiskRules.TextMaxLength);
        RuleFor(x => x.Owner).MaximumLength(RiskRules.OwnerMaxLength);
    }
}

internal sealed class CreateRiskValidator : RiskInputValidator<CreateRiskRequest>;

internal sealed class UpdateRiskValidator : RiskInputValidator<UpdateRiskRequest>
{
    public UpdateRiskValidator()
    {
        RuleFor(x => x.Status).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Choose a status.")
            .IsInEnum()
            .NotEqual(RiskStatus.Closed).WithMessage("Use Close to close a risk.");
        RuleFor(x => x.Version).GreaterThan(0);
    }
}

internal sealed class CloseRiskValidator : AbstractValidator<CloseRiskRequest>
{
    public CloseRiskValidator()
    {
        RuleFor(x => x.Version).GreaterThan(0);
        RuleFor(x => x.Note).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Explain why the risk is being closed.")
            .MaximumLength(RiskRules.ClosingNoteMaxLength);
    }
}

internal sealed class ReopenRiskValidator : AbstractValidator<ReopenRiskRequest>
{
    public ReopenRiskValidator() => RuleFor(x => x.Version).GreaterThan(0);
}
```

`Features/RisksProblems.cs`:
```csharp
using Microsoft.AspNetCore.Http.HttpResults;
using PDS.Shared.Web;

namespace PDS.Risks.Features;

internal static class RisksProblems
{
    public static ValidationProblem ProjectArchived() =>
        Problems.Field("projectId", "Risks can't be added to an archived project.");

    public static ValidationProblem RiskClosed() =>
        Problems.Field("status", "Reopen the risk before editing it.");

    public static ProblemHttpResult VersionConflict() => Problems.Conflict(
        "This risk was changed by someone else.",
        "Reload the risks to see the latest changes, then apply your edits again.");
}
```

`Features/ProjectLookup.cs`:
```csharp
using PDS.Portfolio.Contracts;

namespace PDS.Risks.Features;

internal static class ProjectLookup
{
    /// <summary>The project's summary, or null when it does not exist.</summary>
    public static async Task<ProjectSummary?> FindAsync(IPortfolioQueries portfolio, Guid projectId, CancellationToken ct) =>
        (await portfolio.GetSummaries([new ProjectId(projectId)], ct)).FirstOrDefault();
}
```

- [ ] **Step 5: Endpoints**

Each endpoint is one file, following the Portfolio feature files (usings: `Microsoft.AspNetCore.Builder`, `Microsoft.AspNetCore.Http`, `Microsoft.AspNetCore.Http.HttpResults`, `Microsoft.AspNetCore.Routing`, plus module and shared namespaces as needed). Handlers are shown below; `Map` methods attach them to the group (whose route already contains `{projectId:guid}`).

`Features/ListRisks.cs`:
```csharp
internal static class ListRisks
{
    public static void Map(RouteGroupBuilder group) => group.MapGet("", Handle).WithName("ListRisks");

    internal static async Task<Results<Ok<List<RiskDetails>>, NotFound>> Handle(
        Guid projectId, bool? includeClosed, RiskStore store, IPortfolioQueries portfolio, CancellationToken ct)
    {
        if (await ProjectLookup.FindAsync(portfolio, projectId, ct) is null)
            return TypedResults.NotFound();

        var risks = await store.ListForProjectAsync(new ProjectId(projectId), ct);
        var ordered = risks
            .Where(r => includeClosed == true || r.Status != RiskStatus.Closed)
            .OrderBy(r => r.Status == RiskStatus.Closed)
            .ThenByDescending(r => r.Score)
            .ThenBy(r => r.Fields.Title, StringComparer.OrdinalIgnoreCase)
            .Select(RiskDetails.From)
            .ToList();
        return TypedResults.Ok(ordered);
    }
}
```

`Features/GetRisk.cs`:
```csharp
internal static class GetRisk
{
    public static void Map(RouteGroupBuilder group) => group.MapGet("/{riskId:guid}", Handle).WithName("GetRisk");

    internal static async Task<Results<Ok<RiskDetails>, NotFound>> Handle(
        Guid projectId, Guid riskId, RiskStore store, CancellationToken ct)
    {
        var risk = await store.GetAsync(new ProjectId(projectId), new RiskId(riskId), ct);
        return risk is null ? TypedResults.NotFound() : TypedResults.Ok(RiskDetails.From(risk));
    }
}
```

`Features/CreateRisk.cs`:
```csharp
internal static class CreateRisk
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<CreateRiskRequest>()
            .WithName("CreateRisk");

    internal static async Task<Results<Created<RiskDetails>, NotFound, ValidationProblem>> Handle(
        Guid projectId, CreateRiskRequest request, RiskStore store, IPortfolioQueries portfolio,
        ICurrentUser user, TimeProvider clock, CancellationToken ct)
    {
        var project = await ProjectLookup.FindAsync(portfolio, projectId, ct);
        if (project is null)
            return TypedResults.NotFound();
        if (project.IsArchived)
            return RisksProblems.ProjectArchived();

        var risk = Risk.Create(new ProjectId(projectId), request.ToFields(), new AuditStamp(clock.GetUtcNow(), user.Name));
        await store.CreateAsync(risk, ct);
        return TypedResults.Created($"/api/risks/projects/{projectId}/risks/{risk.Id.Value}", RiskDetails.From(risk));
    }
}
```

`Features/UpdateRisk.cs`:
```csharp
internal static class UpdateRisk
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPut("/{riskId:guid}", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<UpdateRiskRequest>()
            .WithName("UpdateRisk");

    internal static async Task<Results<Ok<RiskDetails>, NotFound, ValidationProblem, ProblemHttpResult>> Handle(
        Guid projectId, Guid riskId, UpdateRiskRequest request, RiskStore store, ICurrentUser user,
        TimeProvider clock, CancellationToken ct)
    {
        var risk = await store.GetAsync(new ProjectId(projectId), new RiskId(riskId), ct);
        if (risk is null)
            return TypedResults.NotFound();
        if (risk.Version != request.Version)
            return RisksProblems.VersionConflict();
        if (risk.Status == RiskStatus.Closed)
            return RisksProblems.RiskClosed();

        risk.Update(request.ToFields(), request.Status!.Value, new AuditStamp(clock.GetUtcNow(), user.Name));
        return await store.UpdateAsync(risk, request.Version, ct)
            ? TypedResults.Ok(RiskDetails.From(risk))
            : RisksProblems.VersionConflict();
    }
}
```

`Features/CloseRisk.cs` (close and reopen):
```csharp
internal static class CloseRisk
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{riskId:guid}/close", Close)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<CloseRiskRequest>()
            .WithName("CloseRisk");
        group.MapPost("/{riskId:guid}/reopen", Reopen)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<ReopenRiskRequest>()
            .WithName("ReopenRisk");
    }

    internal static Task<Results<Ok<RiskDetails>, NotFound, ProblemHttpResult>> Close(
        Guid projectId, Guid riskId, CloseRiskRequest request, RiskStore store, ICurrentUser user,
        TimeProvider clock, CancellationToken ct) =>
        Change(projectId, riskId, request.Version, store, (risk, stamp) => risk.Close(request.Note, stamp), user, clock, ct);

    internal static Task<Results<Ok<RiskDetails>, NotFound, ProblemHttpResult>> Reopen(
        Guid projectId, Guid riskId, ReopenRiskRequest request, RiskStore store, ICurrentUser user,
        TimeProvider clock, CancellationToken ct) =>
        Change(projectId, riskId, request.Version, store, (risk, stamp) => risk.Reopen(stamp), user, clock, ct);

    private static async Task<Results<Ok<RiskDetails>, NotFound, ProblemHttpResult>> Change(
        Guid projectId, Guid riskId, long version, RiskStore store, Func<Risk, AuditStamp, bool> apply,
        ICurrentUser user, TimeProvider clock, CancellationToken ct)
    {
        var risk = await store.GetAsync(new ProjectId(projectId), new RiskId(riskId), ct);
        if (risk is null)
            return TypedResults.NotFound();
        if (risk.Version != version)
            return RisksProblems.VersionConflict();
        if (!apply(risk, new AuditStamp(clock.GetUtcNow(), user.Name)))
            return TypedResults.Ok(RiskDetails.From(risk));

        return await store.UpdateAsync(risk, version, ct)
            ? TypedResults.Ok(RiskDetails.From(risk))
            : RisksProblems.VersionConflict();
    }
}
```

- [ ] **Step 6: Module registration and host wiring**

`RisksModule.cs`:
```csharp
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PDS.Risks.Data;
using PDS.Risks.Features;
using PDS.Shared.Data;
using PDS.Shared.Security;

namespace PDS.Risks;

public static class RisksModule
{
    /// <summary>Tables this module owns. The host creates them locally; CDK creates them in AWS.</summary>
    public static IReadOnlyList<TableDefinition> Tables { get; } = [RisksTable.Definition];

    public static IServiceCollection AddRisksModule(this IServiceCollection services)
    {
        services.AddSingleton(RisksTable.Definition);
        services.AddSingleton<RiskStore>();
        services.AddValidatorsFromAssemblyContaining<CreateRiskValidator>(includeInternalTypes: true);
        return services;
    }

    public static IEndpointRouteBuilder MapRisksEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/risks/projects/{projectId:guid}/risks")
            .WithTags("Risks")
            .RequireAuthorization(Policies.CanRead);
        ListRisks.Map(group);
        GetRisk.Map(group);
        CreateRisk.Map(group);
        UpdateRisk.Map(group);
        CloseRisk.Map(group);
        return app;
    }
}
```

- `src/PDS.Api/PDS.Api.csproj`: add `<ProjectReference Include="../Modules/PDS.Risks/PDS.Risks.csproj" />`.
- `src/PDS.Api/Program.cs`: add `using PDS.Risks;`, then `builder.Services.AddRisksModule();` after `AddPortfolioModule()`, and `app.MapRisksEndpoints();` after `app.MapPortfolioEndpoints();`.
- `infra/PDS.Infra/PDS.Infra.csproj`: add `<ProjectReference Include="../../src/Modules/PDS.Risks/PDS.Risks.csproj" />`.
- `infra/PDS.Infra/Program.cs`: add `using PDS.Risks;` and change the call to `PdsApp.Define(app, settings, assets, [.. PortfolioModule.Tables, .. RisksModule.Tables]);`.

- [ ] **Step 7: Build, contract and manual check**

Run: `dotnet format PDS.slnx && dotnet build PDS.slnx`. Expected: 0 warnings, and `web/src/shared/api/openapi.json` now contains `/api/risks/projects/{projectId}/risks` paths.
Run in web/: `npm run gen:api`. Expected: `schema.d.ts` updated. Commit both generated files.
Manual check: start `docker compose up -d dynamodb` and the API (Development), then use curl.
- Create a project.
- POST 2 risks to it, rated (2,2) and (4,5). Listing returns the Extreme 20 risk first.
- POST without `likelihood`: expect 400 on `likelihood`.
- PUT with `status: "Closed"`: expect 400 on `status`.
- Close with an empty note: expect 400 on `note`. Close with a note: status Closed.
- The default list hides the closed risk; `?includeClosed=true` shows it.
- PUT on the closed risk: expect 400 on `status`. Reopen, then PUT with a stale version: expect 409.
- Archive the project (dev user is Admin), then POST a risk: expect 400 on `projectId`.
- List risks for a random project id: expect 404.

Stop everything with `docker compose down`.
Infra: run `dotnet publish src/PDS.Api -c Release -r linux-arm64 --self-contained false -p:PublishReadyToRun=true -o artifacts/api`, then in infra/ run `npx --yes aws-cdk@2.1143.0 synth -c deployment=example-dev --quiet`. Confirm `cdk.out/example-dev-data.template.json` has a second `AWS::DynamoDB::Table` named `example-dev-risks`, and that the app template's Lambda env contains `Tables__risks`.

- [ ] **Step 8: Commit**

```bash
git add src/Modules/PDS.Risks PDS.slnx src/PDS.Api infra/PDS.Infra web/src/shared/api
git commit -m "feat(risks): risk registry module with per-project risks API"
```

---

### Task 2: Risks section on the project detail page

**Files:**
- Create: `web/src/features/risks/types.ts`, `api.ts`, `RiskBandBadge.tsx`, `riskFormModel.ts`, `RiskFormModal.tsx`, `CloseRiskModal.tsx`, `ProjectRisksSection.tsx`
- Modify: `web/src/features/portfolio/ProjectDetailPage.tsx` (render the section)

**Interfaces:**
- Consumes: the Task 1 API and the generated `schema.d.ts`; shared `useApi`, `unwrap`, `ApiError`, `DeepRequired`, `usePermissions`, `useConfig`, `formatDate`, `zodValidate`, `serverFieldErrors`.
- Produces: `<ProjectRisksSection projectId={string} projectArchived={boolean} />`.

- [ ] **Step 1: Types, labels and hooks**

`web/src/features/risks/types.ts`:
```ts
import type { paths } from '../../shared/api/schema';
import type { DeepRequired } from '../../shared/api/types';

type RisksPath = paths['/api/risks/projects/{projectId}/risks'];
type RiskPath = paths['/api/risks/projects/{projectId}/risks/{riskId}'];

export type RiskDetails = DeepRequired<RisksPath['get']['responses'][200]['content']['application/json']>[number];
export type RiskInput = RisksPath['post']['requestBody']['content']['application/json'];
export type RiskUpdateInput = RiskPath['put']['requestBody']['content']['application/json'];

export const categories = [
  'Geotechnical', 'Consenting', 'Contractual', 'ContractorDefault', 'Market',
  'Financial', 'Design', 'HealthAndSafety', 'Environmental', 'Other',
] as const;
export type Category = (typeof categories)[number];

export const categoryLabel: Record<Category, string> = {
  Geotechnical: 'Geotechnical',
  Consenting: 'Consenting',
  Contractual: 'Contractual / dispute',
  ContractorDefault: 'Contractor default',
  Market: 'Market',
  Financial: 'Financial',
  Design: 'Design',
  HealthAndSafety: 'Health & safety',
  Environmental: 'Environmental',
  Other: 'Other',
};

export type Band = 'Low' | 'Medium' | 'High' | 'Extreme';
export const bandColor: Record<Band, string> = { Low: 'green', Medium: 'yellow', High: 'orange', Extreme: 'red' };

export type RiskStatus = 'Open' | 'Mitigating' | 'Closed';
export const statusLabel: Record<RiskStatus, string> = { Open: 'Open', Mitigating: 'Mitigating', Closed: 'Closed' };

export const likelihoodLabels = ['Rare', 'Unlikely', 'Possible', 'Likely', 'Almost certain'] as const;
export const impactLabels = ['Insignificant', 'Minor', 'Moderate', 'Major', 'Severe'] as const;

/** Mirrors the server's RiskRules.BandFor. */
export function bandFor(score: number): Band {
  if (score <= 4) return 'Low';
  if (score <= 9) return 'Medium';
  if (score <= 16) return 'High';
  return 'Extreme';
}
```
If the generated enum unions differ from these literal unions, make the local types equal the generated ones. Don't cast in components.

`web/src/features/risks/api.ts`:
```ts
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { unwrap, useApi } from '../../shared/api/client';
import type { RiskDetails, RiskInput, RiskUpdateInput } from './types';

const risksKey = (projectId: string) => ['risks', projectId];

function useRefreshRisks(projectId: string) {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: risksKey(projectId) });
}

export function useRisks(projectId: string, includeClosed: boolean) {
  const api = useApi();
  return useQuery({
    queryKey: [...risksKey(projectId), { includeClosed }],
    queryFn: () =>
      unwrap<RiskDetails[]>(
        api.GET('/api/risks/projects/{projectId}/risks', { params: { path: { projectId }, query: { includeClosed } } }),
      ),
  });
}

export function useCreateRisk(projectId: string) {
  const api = useApi();
  const refresh = useRefreshRisks(projectId);
  return useMutation({
    mutationFn: (body: RiskInput) =>
      unwrap<RiskDetails>(api.POST('/api/risks/projects/{projectId}/risks', { params: { path: { projectId } }, body })),
    onSettled: refresh,
  });
}

export function useUpdateRisk(projectId: string) {
  const api = useApi();
  const refresh = useRefreshRisks(projectId);
  return useMutation({
    mutationFn: ({ riskId, body }: { riskId: string; body: RiskUpdateInput }) =>
      unwrap<RiskDetails>(
        api.PUT('/api/risks/projects/{projectId}/risks/{riskId}', { params: { path: { projectId, riskId } }, body }),
      ),
    onSettled: refresh,
  });
}

export function useCloseRisk(projectId: string) {
  const api = useApi();
  const refresh = useRefreshRisks(projectId);
  return useMutation({
    mutationFn: ({ riskId, version, note }: { riskId: string; version: number; note: string }) =>
      unwrap<RiskDetails>(
        api.POST('/api/risks/projects/{projectId}/risks/{riskId}/close', {
          params: { path: { projectId, riskId } },
          body: { version, note },
        }),
      ),
    onSettled: refresh,
  });
}

export function useReopenRisk(projectId: string) {
  const api = useApi();
  const refresh = useRefreshRisks(projectId);
  return useMutation({
    mutationFn: ({ riskId, version }: { riskId: string; version: number }) =>
      unwrap<RiskDetails>(
        api.POST('/api/risks/projects/{projectId}/risks/{riskId}/reopen', {
          params: { path: { projectId, riskId } },
          body: { version },
        }),
      ),
    onSettled: refresh,
  });
}
```
`onSettled` refreshes after both success and failure, so a 409 always leaves the list showing the latest data.

`web/src/features/risks/RiskBandBadge.tsx`:
```tsx
import { Badge } from '@mantine/core';
import { bandColor, type Band } from './types';

export function RiskBandBadge({ band, score }: { band: Band; score: number }) {
  return (
    <Badge color={bandColor[band]} variant="filled" style={{ fontVariantNumeric: 'tabular-nums' }}>
      {band} · {score}
    </Badge>
  );
}
```

- [ ] **Step 2: Form model**

`web/src/features/risks/riskFormModel.ts`:
```ts
import { z } from 'zod';
import { categories, type Category, type RiskDetails, type RiskInput } from './types';

export type RiskFormValues = {
  title: string;
  category: Category | null;
  likelihood: string | null; // '1'..'5' (Mantine Select values are strings)
  impact: string | null;
  owner: string;
  dueDate: string;
  status: 'Open' | 'Mitigating';
  description: string;
  mitigation: string;
};

export const emptyRiskValues: RiskFormValues = {
  title: '', category: null, likelihood: null, impact: null, owner: '', dueDate: '',
  status: 'Open', description: '', mitigation: '',
};

const rating = (label: string) =>
  z.string({ error: `Choose ${label}` }).regex(/^[1-5]$/, `Choose ${label}`);

/** Mirrors the API's RiskInputValidator. */
export const riskSchema = z.object({
  title: z.string().trim().min(1, 'Title is required').max(200, 'Use 200 characters or fewer'),
  category: z.enum(categories, { error: 'Choose a category' }),
  likelihood: rating('a likelihood'),
  impact: rating('an impact'),
  owner: z.string().max(200, 'Use 200 characters or fewer'),
  dueDate: z.string().refine((v) => v === '' || /^\d{4}-\d{2}-\d{2}$/.test(v), 'Enter a valid date'),
  status: z.enum(['Open', 'Mitigating']),
  description: z.string().max(4000, 'Use 4000 characters or fewer'),
  mitigation: z.string().max(4000, 'Use 4000 characters or fewer'),
});

const orNull = (v: string) => (v.trim() === '' ? null : v.trim());

export function toRiskInput(v: RiskFormValues): RiskInput {
  return {
    title: v.title.trim(),
    category: v.category,
    likelihood: v.likelihood === null ? null : Number(v.likelihood),
    impact: v.impact === null ? null : Number(v.impact),
    owner: orNull(v.owner),
    dueDate: orNull(v.dueDate),
    description: orNull(v.description),
    mitigation: orNull(v.mitigation),
  };
}

export function fromRisk(r: RiskDetails): RiskFormValues {
  return {
    title: r.title,
    category: r.category,
    likelihood: String(r.likelihood),
    impact: String(r.impact),
    owner: r.owner ?? '',
    dueDate: r.dueDate ?? '',
    status: r.status === 'Mitigating' ? 'Mitigating' : 'Open',
    description: r.description ?? '',
    mitigation: r.mitigation ?? '',
  };
}
```

- [ ] **Step 3: Modals**

`web/src/features/risks/RiskFormModal.tsx`: a Mantine `Modal`, open when `risk !== undefined`, where `risk` is `RiskDetails | 'new' | undefined`.
- **Title:** "Add risk" or "Edit risk".
- **Form:** `useForm<RiskFormValues>({ mode: 'controlled', initialValues, validate: zodValidate(riskSchema) })`, with `initialValues` from `fromRisk(risk)` or `emptyRiskValues`. Key the component on the risk id so it resets.
- **Fields, in order:**
  - Title (withAsterisk)
  - Category (Select, data from `categories`/`categoryLabel`)
  - Likelihood and Impact side by side: Selects with data `1..5` labelled `"<n> – <label>"` from `likelihoodLabels`/`impactLabels`
  - Live preview: `<RiskBandBadge>` when both are chosen, using `bandFor(l*i)`, otherwise dimmed text "Choose likelihood and impact"
  - Owner
  - Due date (`TextInput type="date"`)
  - Status (SegmentedControl Open/Mitigating), shown only when editing
  - Description and Mitigation (Textarea, autosize)
- **Submit:**
  - Create calls `useCreateRisk(projectId).mutateAsync(toRiskInput(values))`.
  - Update calls `useUpdateRisk(projectId).mutateAsync({ riskId, body: { ...toRiskInput(values), status: values.status, version: risk.version } })`.
  - On success, call `notifications.show({ color: 'green', message: 'Risk saved' })` and `onClose()`.
- **Errors:**
  - `ApiError` 400 with `problem.errors` → `form.setErrors(serverFieldErrors(error.problem))`. If the only key is `projectId` or `status`, also show it as an `Alert` at the top of the form, since there's no such field.
  - 409 → `notifications.show({ color: 'yellow', title: 'This risk was changed by someone else', message: 'The list now shows the latest version. Open it again to make your changes.' })`, then `onClose()`.
  - Anything else → a red notification with the error message.
- **Buttons:** "Save" (loading while pending) and "Cancel".

`web/src/features/risks/CloseRiskModal.tsx`: a Modal "Close risk".
- **Content:** the risk title in bold, then a required `Textarea` "Why is this risk being closed?" (max 2000) validated with `z.string().trim().min(1, 'Explain why the risk is being closed')`.
- **Submit:** calls `useCloseRisk(projectId).mutateAsync({ riskId, version, note })`.
- **Errors:** the same 400/409 handling as the form modal.
- **Buttons:** "Close risk" (color red) and "Cancel".

- [ ] **Step 4: Section and detail-page wiring**

`web/src/features/risks/ProjectRisksSection.tsx`:
```tsx
import { Alert, Button, Card, Group, Loader, Switch, Table, Text, Title } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useState } from 'react';
import { ApiError } from '../../shared/api/client';
import { useConfig } from '../../shared/config';
import { formatDate } from '../../shared/format';
import { usePermissions } from '../../shared/me';
import { useReopenRisk, useRisks } from './api';
import { CloseRiskModal } from './CloseRiskModal';
import { RiskBandBadge } from './RiskBandBadge';
import { RiskFormModal } from './RiskFormModal';
import { categoryLabel, statusLabel, type RiskDetails } from './types';

export function ProjectRisksSection({ projectId, projectArchived }: { projectId: string; projectArchived: boolean }) {
  const config = useConfig();
  const { canWrite } = usePermissions();
  const [showClosed, setShowClosed] = useState(false);
  const [editing, setEditing] = useState<RiskDetails | 'new' | undefined>(undefined);
  const [closing, setClosing] = useState<RiskDetails | undefined>(undefined);
  const risks = useRisks(projectId, showClosed);
  const reopen = useReopenRisk(projectId);

  const onReopen = (risk: RiskDetails) =>
    reopen.mutate(
      { riskId: risk.id, version: risk.version },
      {
        onError: (error) =>
          notifications.show({
            color: 'red',
            title: error instanceof ApiError && error.status === 409 ? 'This risk was changed by someone else' : 'Could not reopen the risk',
            message: 'The list now shows the latest version.',
          }),
      },
    );

  return (
    <Card withBorder>
      <Group justify="space-between" mb="sm">
        <Title order={4}>Risks</Title>
        <Group>
          <Switch label="Show closed" checked={showClosed} onChange={(e) => setShowClosed(e.currentTarget.checked)} />
          {canWrite && !projectArchived && <Button onClick={() => setEditing('new')}>Add risk</Button>}
        </Group>
      </Group>

      {risks.error ? (
        <Alert color="red" title="Could not load risks">{risks.error.message}</Alert>
      ) : !risks.data ? (
        <Loader size="sm" />
      ) : risks.data.length === 0 ? (
        <Text c="dimmed">{showClosed ? 'No risks recorded.' : 'No open risks recorded.'}</Text>
      ) : (
        <Table.ScrollContainer minWidth={720}>
          <Table highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Rating</Table.Th>
                <Table.Th>Title</Table.Th>
                <Table.Th>Category</Table.Th>
                <Table.Th>Owner</Table.Th>
                <Table.Th>Due</Table.Th>
                <Table.Th>Status</Table.Th>
                {canWrite && <Table.Th />}
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {risks.data.map((risk) => (
                <Table.Tr key={risk.id} opacity={risk.status === 'Closed' ? 0.6 : 1}>
                  <Table.Td><RiskBandBadge band={risk.band} score={risk.score} /></Table.Td>
                  <Table.Td>
                    <Text fw={500}>{risk.title}</Text>
                    {risk.mitigation && <Text size="sm" c="dimmed" lineClamp={1}>{risk.mitigation}</Text>}
                  </Table.Td>
                  <Table.Td>{categoryLabel[risk.category]}</Table.Td>
                  <Table.Td>{risk.owner ?? '—'}</Table.Td>
                  <Table.Td>{formatDate(risk.dueDate, config.culture)}</Table.Td>
                  <Table.Td>{statusLabel[risk.status]}</Table.Td>
                  {canWrite && (
                    <Table.Td>
                      <Group gap="xs" justify="flex-end" wrap="nowrap">
                        {risk.status === 'Closed' ? (
                          <Button size="xs" variant="default" loading={reopen.isPending} onClick={() => onReopen(risk)}>Reopen</Button>
                        ) : (
                          <>
                            <Button size="xs" variant="light" onClick={() => setEditing(risk)}>Edit</Button>
                            <Button size="xs" variant="default" onClick={() => setClosing(risk)}>Close</Button>
                          </>
                        )}
                      </Group>
                    </Table.Td>
                  )}
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      )}

      <RiskFormModal projectId={projectId} risk={editing} onClose={() => setEditing(undefined)} />
      <CloseRiskModal projectId={projectId} risk={closing} onClose={() => setClosing(undefined)} />
    </Card>
  );
}
```

In `web/src/features/portfolio/ProjectDetailPage.tsx`, import `ProjectRisksSection` from `'../risks/ProjectRisksSection'`. Render `<ProjectRisksSection projectId={p.id} projectArchived={p.isArchived} />` immediately before the audit `<Text size="sm" c="dimmed">Created by …` line.

- [ ] **Step 5: Verify**

In web/, run `npm run typecheck && npm run lint && npm run build`. Expected: all pass.
Manual check:
- Run DynamoDB, the API and `npm run dev`. Through the Vite proxy (`http://localhost:5173/api/...`), create a project and a risk with curl, and list it.
- Confirm `curl -s http://localhost:5173/projects/<id>` serves the SPA.
- Stop everything.

- [ ] **Step 6: Commit**

```bash
git add web/src/features/risks web/src/features/portfolio/ProjectDetailPage.tsx
git commit -m "feat(web): risks section on the project detail page"
```

## Spec coverage

| Spec section | Task |
|---|---|
| §2 Module architecture, contracts-only reference, host lines, infra table list | 1 (steps 1, 6) |
| §3 Data and access patterns | 1 (steps 2, 3) |
| §4 API and rules | 1 (steps 4, 5) |
| §5 UI | 2 |
| Review Focus 1–5 | 1 (UpdateRisk closed check, CreateRisk archived/404, ListRisks 404 and ordering, version checks); 2 (409 handling) |
