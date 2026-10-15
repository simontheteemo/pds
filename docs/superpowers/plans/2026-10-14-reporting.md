# PDS Reporting Export (v1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Portfolio-wide CSV downloads (8 datasets) from a new Reports page, generated on demand by the API, Manager/Admin only.

**Architecture:** A new module `src/Modules/PDS.Reporting` (no tables) publishes `IExportDataset` / `ExportTable` / `ExportColumn<T>` in its `Contracts`, and serves `/api/reporting/exports` (catalog plus one CSV download per dataset) behind its own `CanExport` policy. Each data module (Portfolio, Risks, Contractors, Costs, Consents, Sales) implements its datasets internally against its own store and registers them with `services.AddSingleton<IExportDataset, …>()`. Reporting enumerates `IEnumerable<IExportDataset>` and never references a data module. The web app adds `web/src/features/reporting` with a `/reports` page.

**Tech Stack:** Same as the foundation (.NET 10 Minimal APIs, AWS SDK DynamoDB; React, Mantine 9, TanStack Query, openapi-fetch). No new packages.

**Spec:** `docs/superpowers/specs/2026-10-14-reporting-design.md`. Reference implementations:
- `src/Modules/PDS.Risks` for the module shape
- `src/Modules/PDS.Consents/Features/ListDue.cs` for portfolio-wide reads with project names
- `src/Modules/PDS.Costs/Features/GetCostReport.cs` for cost figures and the deleted-line and unknown-contract labels
- `web/src/features/consents/ConsentsDuePage.tsx` for a top-level page

## Global Constraints

- All foundation constraints apply: net10.0, central package versions (**no new packages**), and only `Contracts/` and `ReportingModule` are public in `PDS.Reporting`. Dataset classes inside data modules are `internal sealed`.
- **Dependency direction:** each data module adds `<ProjectReference Include="../PDS.Reporting/PDS.Reporting.csproj" />`, with the comment `<!-- Only PDS.Reporting.Contracts types may be used (module rule). -->`. `PDS.Reporting` references only `PDS.Shared`. No module reads another module's table. No new interfaces are added to any other module's `Contracts`.
- **No changes** to `PDS.Shared`, `src/PDS.Api/Security`, `infra/` or `web/src/shared` (other than the generated `openapi.json` / `schema.d.ts`).
- **Dataset names:** kebab-case and stable: `projects`, `risks`, `contracts`, `cost-lines`, `cost-entries`, `consents`, `consent-conditions`, `units`.
- **Column names:** `snake_case`, exactly as listed in spec §3, in that order.
- **Values:** pass typed values (`DateOnly`, `DateTimeOffset`, `decimal`, `int`, `bool`, enums, `Guid`) to the CSV writer. Never pre-format them as strings. Ids are exported as `Guid` (`x.Id.Value`).
- **Every child dataset** starts with its own id, then `project_id, project_code, project_name`. Code and name come from `IPortfolioQueries.GetSummaries`. A missing project leaves code and name `null`.
- **Not exported:** buyer email and buyer phone.
- **No new tests** (user directive). Verify with:
  - `dotnet format PDS.slnx`, then `dotnet build PDS.slnx` (0 warnings). Existing tests must still compile.
  - In `web/`: `npm run gen:api && npm run typecheck && npm run lint && npm run build`.
  - In `infra/`: `npx aws-cdk@2.1143.0 synth -c deployment=example-dev` (needs `artifacts/api` and `web/dist`; see README "Build the artifacts"). Expect no infra diff.
  - The manual checks each task names.
- **Ports:** run the API with `dotnet run --project src/PDS.Api --urls http://localhost:5088`. Vite runs on 5198. The committed `vite.config.ts` proxies to 5080, so for manual checks create an **untracked** `web/vite.reporting.config.ts` (port 5198, proxying `/api` to `http://localhost:5088`), and delete it afterwards. DynamoDB Local is shared on :8000: reuse it, never stop it.
- **Shared wiring files:** keep edits minimal and additive, one line each (`PDS.slnx`, `src/PDS.Api/PDS.Api.csproj`, `src/PDS.Api/Program.cs`, `web/src/routes.tsx`, `web/src/shared/layout/AppLayout.tsx`).
- **Commits:** conventional (`feat(reporting): …`, `feat(web): …`), ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **CSV correctness in Excel.** Text containing a comma, a double quote or a newline (e.g. multi-line risk mitigation) must stay in one cell. The file must start with the UTF-8 BOM so "Tāmaki" shows correctly. Negative amounts (credit actuals) must stay numeric (no `'` prefix). Only text starting with `= + - @ \t \r` gets the `'` prefix.
2. **Invariant formatting.** Decimals use `.` and no thousands separators whatever the server culture. `DateOnly` is `yyyy-MM-dd`. `DateTimeOffset` is converted to UTC `yyyy-MM-ddTHH:mm:ssZ`. Nulls are empty cells, never "null".
3. **Permissions.** A Viewer (dev auth roles `["Viewer"]`) gets 403 on both `/api/reporting/exports` and `/api/reporting/exports/risks`. A Manager gets 200. An unknown name gets 404 (not 500).
4. **Cost figures match the cost report.** For a project, `cost-lines` budget, committed, actual, FFC, FTC and variance equal `GET /api/costs/projects/{id}` line figures. Entries on a deleted line show `(deleted line)`; an entry linked to a missing contract shows `(unknown contract)`.
5. **Portfolio-wide, including archived and orphaned rows.** Every dataset includes archived projects' rows. A row whose project id is unknown still appears, with blank code and name. Datasets with zero rows still return the header line.

---

### Task 1: Reporting module — export contract, CSV writer, catalog, endpoints, `projects` dataset

**Files:**
- Create: `src/Modules/PDS.Reporting/PDS.Reporting.csproj`, `ReportingModule.cs`
- Create: `Contracts/IExportDataset.cs`, `Contracts/ExportTable.cs`
- Create: `Features/CsvWriter.cs`, `Features/ExportCatalog.cs`, `Features/ListExports.cs`, `Features/DownloadExport.cs`
- Create: `src/Modules/PDS.Portfolio/Features/ProjectsExport.cs`
- Modify: `src/Modules/PDS.Portfolio/PDS.Portfolio.csproj` (add the Reporting reference), `src/Modules/PDS.Portfolio/PortfolioModule.cs` (one `AddSingleton` line)
- Modify (one line each): `PDS.slnx`, `src/PDS.Api/PDS.Api.csproj`, `src/PDS.Api/Program.cs` (using + Add + Map)
- Generated: `web/src/shared/api/openapi.json`, `web/src/shared/api/schema.d.ts`

**Interfaces:**
- Produces (public, used by Tasks 2–3):
  - `PDS.Reporting.Contracts.IExportDataset { string Name; string Title; string Description; Task<ExportTable> LoadAsync(CancellationToken) }`
  - `ExportColumn<T>(string Name, Func<T, object?> Value)`
  - `ExportTable.From<T>(IEnumerable<T>, IReadOnlyList<ExportColumn<T>>)`
- Produces (public): `ReportingModule.{AddReportingModule, MapReportingEndpoints}`, `ReportingPolicies.CanExport`
- HTTP operations: `ListExports` (`GET /api/reporting/exports` → `ExportDatasetInfo[]`) and `DownloadExport` (`GET /api/reporting/exports/{name}` → `text/csv`)

- [ ] **Step 1: Project and contracts**

`PDS.Reporting.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../PDS.Shared/PDS.Shared.csproj" />
  </ItemGroup>
</Project>
```
Add it to `PDS.slnx` under `/src/Modules/` (alphabetical), and add a `ProjectReference` from `src/PDS.Api/PDS.Api.csproj` next to the other modules.

```csharp
// Contracts/IExportDataset.cs
namespace PDS.Reporting.Contracts;

/// <summary>
/// A portfolio-wide table a module exports from its own data. Register with
/// <c>services.AddSingleton&lt;IExportDataset, MyDataset&gt;()</c> in the module's Add method.
/// </summary>
public interface IExportDataset
{
    /// <summary>Stable kebab-case id: URL segment and file name (later the S3 prefix and Athena table).</summary>
    string Name { get; }

    string Title { get; }

    string Description { get; }

    Task<ExportTable> LoadAsync(CancellationToken ct);
}
```

```csharp
// Contracts/ExportTable.cs
namespace PDS.Reporting.Contracts;

/// <summary>One output column: a snake_case name and a typed value (null, string, bool, number, DateOnly, DateTimeOffset, Guid or enum).</summary>
public sealed record ExportColumn<T>(string Name, Func<T, object?> Value);

public sealed class ExportTable
{
    private ExportTable(IReadOnlyList<string> columns, IReadOnlyList<object?[]> rows)
    {
        Columns = columns;
        Rows = rows;
    }

    public IReadOnlyList<string> Columns { get; }

    public IReadOnlyList<object?[]> Rows { get; }

    public static ExportTable From<T>(IEnumerable<T> rows, IReadOnlyList<ExportColumn<T>> columns) =>
        new(
            columns.Select(c => c.Name).ToList(),
            rows.Select(r => columns.Select(c => c.Value(r)).ToArray()).ToList());
}
```

- [ ] **Step 2: CSV writer**

```csharp
// Features/CsvWriter.cs
using System.Globalization;
using System.Text;
using PDS.Reporting.Contracts;

namespace PDS.Reporting.Features;

/// <summary>RFC 4180 CSV, UTF-8 with BOM (so Excel detects the encoding), CRLF line endings, invariant formats.</summary>
internal static class CsvWriter
{
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    public static byte[] Write(ExportTable table)
    {
        var sb = new StringBuilder();
        AppendRow(sb, table.Columns.Select(c => (object?)c));
        foreach (var row in table.Rows)
            AppendRow(sb, row);

        var preamble = Utf8WithBom.GetPreamble();
        var body = Utf8WithBom.GetBytes(sb.ToString());
        var bytes = new byte[preamble.Length + body.Length];
        preamble.CopyTo(bytes, 0);
        body.CopyTo(bytes, preamble.Length);
        return bytes;
    }

    private static void AppendRow(StringBuilder sb, IEnumerable<object?> values)
    {
        var first = true;
        foreach (var value in values)
        {
            if (!first)
                sb.Append(',');
            first = false;
            sb.Append(Field(value));
        }

        sb.Append("\r\n");
    }

    internal static string Field(object? value) => value switch
    {
        null => string.Empty,
        string s => Quote(GuardFormula(s)),
        bool b => b ? "true" : "false",
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTimeOffset t => t.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
        Enum e => e.ToString(),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => Quote(GuardFormula(value.ToString() ?? string.Empty)),
    };

    /// <summary>Stops spreadsheets evaluating user text as a formula (OWASP CSV injection). Numbers never reach here.</summary>
    private static string GuardFormula(string s) =>
        s.Length > 0 && s[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + s : s;

    private static string Quote(string s) =>
        s.AsSpan().IndexOfAny(",\"\r\n") >= 0 ? "\"" + s.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : s;
}
```

- [ ] **Step 3: Catalog and endpoints**

```csharp
// Features/ExportCatalog.cs
using System.Text.RegularExpressions;
using PDS.Reporting.Contracts;

namespace PDS.Reporting.Features;

/// <summary>Every registered dataset, validated once at startup.</summary>
internal sealed partial class ExportCatalog
{
    private readonly Dictionary<string, IExportDataset> byName;

    public ExportCatalog(IEnumerable<IExportDataset> datasets)
    {
        byName = new Dictionary<string, IExportDataset>(StringComparer.Ordinal);
        foreach (var d in datasets)
        {
            if (!NamePattern().IsMatch(d.Name))
                throw new InvalidOperationException($"Export dataset name '{d.Name}' must be kebab-case.");
            if (!byName.TryAdd(d.Name, d))
                throw new InvalidOperationException($"Export dataset '{d.Name}' is registered twice.");
        }

        All = byName.Values.OrderBy(d => d.Title, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public IReadOnlyList<IExportDataset> All { get; }

    public IExportDataset? Find(string name) => byName.GetValueOrDefault(name);

    [GeneratedRegex("^[a-z][a-z0-9-]*$")]
    private static partial Regex NamePattern();
}
```

```csharp
// Features/ListExports.cs
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace PDS.Reporting.Features;

internal sealed record ExportDatasetInfo(string Name, string Title, string Description);

internal static class ListExports
{
    public static void Map(RouteGroupBuilder group) => group.MapGet("", Handle).WithName("ListExports");

    internal static Ok<List<ExportDatasetInfo>> Handle(ExportCatalog catalog) =>
        TypedResults.Ok(catalog.All.Select(d => new ExportDatasetInfo(d.Name, d.Title, d.Description)).ToList());
}
```

```csharp
// Features/DownloadExport.cs
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PDS.Shared.Security;
using PDS.Shared.Settings;

namespace PDS.Reporting.Features;

internal static class DownloadExport
{
    public const string CsvContentType = "text/csv; charset=utf-8";

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{name}", Handle)
            .WithName("DownloadExport")
            .Produces(StatusCodes.Status200OK, typeof(string), "text/csv")
            .ProducesProblem(StatusCodes.Status404NotFound);

    internal static async Task<IResult> Handle(
        string name, ExportCatalog catalog, ICurrentUser user, TimeProvider clock, IOptions<LocaleOptions> locale,
        ILoggerFactory loggers, HttpContext http, CancellationToken ct)
    {
        if (catalog.Find(name) is not { } dataset)
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Unknown export.");

        var table = await dataset.LoadAsync(ct);
        var bytes = CsvWriter.Write(table);
        loggers.CreateLogger("PDS.Reporting.Export").LogInformation(
            "Export {Dataset} downloaded by {ExportUser}: {Rows} rows", dataset.Name, user.Name, table.Rows.Count);

        var today = LocalDate.Today(clock, locale.Value);
        http.Response.Headers.CacheControl = "no-store";
        return TypedResults.File(bytes, CsvContentType, $"pds-{dataset.Name}-{today:yyyy-MM-dd}.csv");
    }
}
```

Check how the other modules return 404s. If they use `TypedResults.NotFound()` (no body), follow the house style (`Results<FileContentHttpResult, NotFound>`), since the UI only needs the status. Whichever you pick, keep the OpenAPI response for 200 as `text/csv` with a string schema, so openapi-fetch's `parseAs: 'blob'` call type-checks. The `{today:yyyy-MM-dd}` interpolation uses the current culture's calendar; use `today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)` instead.

- [ ] **Step 4: Module class and host wiring**

```csharp
// ReportingModule.cs
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PDS.Reporting.Features;
using PDS.Shared.Security;

namespace PDS.Reporting;

public static class ReportingPolicies
{
    /// <summary>Bulk export of commercial data: Managers and Admins only.</summary>
    public const string CanExport = "CanExport";
}

public static class ReportingModule
{
    public static IServiceCollection AddReportingModule(this IServiceCollection services)
    {
        services.AddSingleton<ExportCatalog>();
        services.AddAuthorizationBuilder()
            .AddPolicy(ReportingPolicies.CanExport, p => p.RequireRole(Roles.Manager, Roles.Admin));
        return services;
    }

    public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reporting/exports")
            .WithTags("Reporting")
            .RequireAuthorization(ReportingPolicies.CanExport);
        ListExports.Map(group);
        DownloadExport.Map(group);
        return app;
    }
}
```

`ExportCatalog` is a singleton built from `IEnumerable<IExportDataset>`. Every dataset is therefore a singleton and may depend only on singletons (stores, `IPortfolioQueries`, `IContractQueries`, `TimeProvider`, `IOptions<>`). Check that `IPortfolioQueries` and `IContractQueries` are registered as singletons; if either isn't, register `ExportCatalog` as scoped instead. Resolving `ExportCatalog` once at startup, so a bad name fails fast, is optional.

`Program.cs`: add `using PDS.Reporting;`, then `builder.Services.AddReportingModule();` after `AddSalesModule()`, and `app.MapReportingEndpoints();` after `MapSalesEndpoints()`.

- [ ] **Step 5: `projects` dataset (Portfolio)**

Add `<ProjectReference Include="../PDS.Reporting/PDS.Reporting.csproj" />` to `PDS.Portfolio.csproj`, with the "Only PDS.Reporting.Contracts" comment. Register `services.AddSingleton<IExportDataset, ProjectsExport>();` in `AddPortfolioModule`.

```csharp
// src/Modules/PDS.Portfolio/Features/ProjectsExport.cs
using PDS.Portfolio.Data;
using PDS.Portfolio.Domain;
using PDS.Reporting.Contracts;

namespace PDS.Portfolio.Features;

internal sealed class ProjectsExport(ProjectStore store, IOptions<LocaleOptions> locale) : IExportDataset
{
    private ExportColumn<Project>[] Columns(string currency) =>
    [
        new("project_id", p => p.Id.Value),
        new("project_code", p => p.Fields.Code),
        new("project_name", p => p.Fields.Name),
        new("stage", p => p.Fields.Stage),
        new("status", p => p.Fields.Status),
        new("archived", p => p.IsArchived),
        new("address_line", p => p.Fields.Site.AddressLine),
        new("suburb", p => p.Fields.Site.Suburb),
        new("city", p => p.Fields.Site.City),
        new("region", p => p.Fields.Site.Region),
        new("postcode", p => p.Fields.Site.Postcode),
        new("legal_description", p => p.Fields.Site.LegalDescription),
        new("title_reference", p => p.Fields.Site.TitleReference),
        new("land_area_sqm", p => p.Fields.Site.LandAreaSqm),
        new("planned_start", p => p.Fields.PlannedStart),
        new("planned_completion", p => p.Fields.PlannedCompletion),
        new("actual_start", p => p.Fields.ActualStart),
        new("actual_completion", p => p.Fields.ActualCompletion),
        new("budget_amount", p => p.Fields.Budget?.Amount),
        new("currency", p => p.Fields.Budget?.Currency ?? currency),
        new("project_manager", p => p.Fields.ProjectManager),
        new("description", p => p.Fields.Description),
        new("created_at", p => p.Created.At),
        new("updated_at", p => p.Updated.At),
    ];

    public string Name => "projects";
    public string Title => "Projects";
    public string Description => "Every project with its stage, status, site, dates and budget.";

    public async Task<ExportTable> LoadAsync(CancellationToken ct)
    {
        var projects = (await store.ListAllAsync(ct)).OrderBy(p => p.Fields.Code, StringComparer.OrdinalIgnoreCase);
        return ExportTable.From(projects, Columns(locale.Value.Currency));
    }
}
```

`Columns` takes the deployment currency as a parameter (make it `static` if the analyzer asks). Add the `using` lines for `Microsoft.Extensions.Options` and `PDS.Shared.Settings`. Check whether `ProjectStore.ListAllAsync` includes archived projects. If it only reads a non-archived index, find the method that returns all of them, or document the gap in the ledger.

- [ ] **Step 6: Verify and commit**

1. `dotnet format PDS.slnx && dotnet build PDS.slnx`, with 0 warnings. The Debug build regenerates `web/src/shared/api/openapi.json`.
2. `cd web && npm run gen:api && npm run typecheck`.
3. Manual check:
   - Run the API on 5088 (Development auth, Manager role, DynamoDB Local).
   - Create a project whose name contains a comma and a macron (`Tāmaki, Stage 1`).
   - `curl -s localhost:5088/api/reporting/exports` returns `[{"name":"projects",…}]`.
   - `curl -si localhost:5088/api/reporting/exports/projects` returns 200, `content-type: text/csv; charset=utf-8`, `content-disposition: attachment; filename=pds-projects-<today>.csv` and `cache-control: no-store`.
   - `curl -s … | head -c 3 | xxd` shows `efbbbf`. The name is quoted.
   - `curl -si …/exports/nope` returns 404.
   - With dev auth roles set to `["Viewer"]` (see `appsettings.Development.json`, or override with `Auth__Development__Roles__0=Viewer`; check the actual key), both endpoints return 403.

```bash
git add -A && git commit -m "feat(reporting): export module, CSV writer, catalog and projects dataset

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `risks`, `contracts` and `units` datasets

**Files:**
- Modify: `src/Modules/PDS.Risks/Data/RiskStore.cs` (add `ScanAllAsync`), `src/Modules/PDS.Risks/PDS.Risks.csproj`, `RisksModule.cs`
- Create: `src/Modules/PDS.Risks/Features/RisksExport.cs`
- Modify: `src/Modules/PDS.Contractors/Data/ContractStore.cs` (add `ScanAllAsync`), `PDS.Contractors.csproj`, `ContractorsModule.cs`
- Create: `src/Modules/PDS.Contractors/Features/ContractsExport.cs`
- Modify: `src/Modules/PDS.Sales/PDS.Sales.csproj`, `SalesModule.cs`
- Create: `src/Modules/PDS.Sales/Features/UnitsExport.cs`

**Interfaces:**
- Consumes: `IExportDataset`, `ExportColumn<T>`, `ExportTable.From` (Task 1); `IPortfolioQueries.GetSummaries(IReadOnlyCollection<ProjectId>, ct) → IReadOnlyList<ProjectSummary>` (existing)
- Produces: datasets `risks`, `contracts`, `units`

Each module gets the Reporting `ProjectReference` (with the comment) and one `services.AddSingleton<IExportDataset, XExport>();` line in its `Add…Module`.

- [ ] **Step 1: Store scans**

`RiskStore.ScanAllAsync(CancellationToken) → IReadOnlyList<Risk>` is a paged `Scan` with `ConsistentRead = true` and `FilterExpression = "begins_with(sk, :prefix)"` (`:prefix = RiskItemMapper.RiskPrefix`). It loops on `LastEvaluatedKey` exactly like `ListForProjectAsync`.

```csharp
/// <summary>Every risk in every project (full scan; for exports).</summary>
public async Task<IReadOnlyList<Risk>> ScanAllAsync(CancellationToken ct)
{
    var risks = new List<Risk>();
    Dictionary<string, AttributeValue>? start = null;
    do
    {
        var response = await dynamo.ScanAsync(new ScanRequest
        {
            TableName = Table,
            FilterExpression = "begins_with(sk, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":prefix"] = Attr.S(RiskItemMapper.RiskPrefix) },
            ConsistentRead = true,
            ExclusiveStartKey = start,
        }, ct);
        risks.AddRange((response.Items ?? []).Select(RiskItemMapper.FromItem));
        start = response.LastEvaluatedKey is { Count: > 0 } next ? next : null;
    }
    while (start is not null);
    return risks;
}
```

`ContractStore.ScanAllAsync` is the same, with `ContractItemMapper.ContractPrefix` and `ContractItemMapper.FromItem`. Contractor profile items have `sk = PROFILE`, so the prefix filter excludes them.

- [ ] **Step 2: `risks` dataset**

```csharp
// src/Modules/PDS.Risks/Features/RisksExport.cs
using PDS.Portfolio.Contracts;
using PDS.Reporting.Contracts;
using PDS.Risks.Data;
using PDS.Risks.Domain;

namespace PDS.Risks.Features;

internal sealed class RisksExport(RiskStore store, IPortfolioQueries portfolio) : IExportDataset
{
    public string Name => "risks";
    public string Title => "Risks";
    public string Description => "Every risk in every project, with its rating, score, band, owner and status.";

    public async Task<ExportTable> LoadAsync(CancellationToken ct)
    {
        var risks = await store.ScanAllAsync(ct);
        var projects = (await portfolio.GetSummaries(risks.Select(r => r.ProjectId).Distinct().ToList(), ct))
            .ToDictionary(p => p.Id);
        var rows = risks
            .Select(r => (Risk: r, Project: projects.GetValueOrDefault(r.ProjectId)))
            .OrderBy(x => x.Project?.Code, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(x => x.Risk.Score)
            .ThenBy(x => x.Risk.Fields.Title, StringComparer.OrdinalIgnoreCase);

        return ExportTable.From<(Risk Risk, ProjectSummary? Project)>(rows,
        [
            new("risk_id", x => x.Risk.Id.Value),
            new("project_id", x => x.Risk.ProjectId.Value),
            new("project_code", x => x.Project?.Code),
            new("project_name", x => x.Project?.Name),
            new("title", x => x.Risk.Fields.Title),
            new("category", x => x.Risk.Fields.Category),
            new("likelihood", x => x.Risk.Fields.Likelihood),
            new("impact", x => x.Risk.Fields.Impact),
            new("score", x => x.Risk.Score),
            new("band", x => x.Risk.Band),
            new("status", x => x.Risk.Status),
            new("owner", x => x.Risk.Fields.Owner),
            new("due_date", x => x.Risk.Fields.DueDate),
            new("description", x => x.Risk.Fields.Description),
            new("mitigation", x => x.Risk.Fields.Mitigation),
            new("closing_note", x => x.Risk.ClosingNote),
            new("closed_at", x => x.Risk.Closed?.At),
            new("created_at", x => x.Risk.Created.At),
            new("updated_at", x => x.Risk.Updated.At),
        ]);
    }
}
```

Note: `x.Risk.Closed?.At` boxes as `DateTimeOffset?`. A null nullable boxes to `null`, and a value boxes to `DateTimeOffset`, so the writer's switch handles both.

- [ ] **Step 3: `contracts` dataset**

`ContractsExport(ContractStore contracts, ContractorStore contractors, IPortfolioQueries portfolio)`:
- Scan the contracts.
- `contractors.GetManyAsync(distinct contractor ids)` → a name dictionary. A missing contractor gets `contractor_name = null`.
- Get the project summaries as in Step 2.
- Order by project code, then contractor name, then title (all `OrdinalIgnoreCase`).
- Columns, in order:

```csharp
new("contract_id", x => x.Contract.Id.Value),
new("project_id", x => x.Contract.ProjectId.Value),
new("project_code", x => x.Project?.Code),
new("project_name", x => x.Project?.Name),
new("contractor_id", x => x.Contract.ContractorId.Value),
new("contractor_name", x => x.ContractorName),
new("title", x => x.Contract.Fields.Title),
new("reference", x => x.Contract.Fields.Reference),
new("form", x => x.Contract.Fields.Form),
new("status", x => x.Contract.Fields.Status),
new("value_amount", x => x.Contract.Fields.Value?.Amount),
new("currency", x => x.Contract.Fields.Value?.Currency ?? currency),   // currency = IOptions<LocaleOptions>.Value.Currency
new("start_date", x => x.Contract.Fields.StartDate),
new("end_date", x => x.Contract.Fields.EndDate),
new("retention_percent", x => x.Contract.Fields.RetentionPercent),
new("notes", x => x.Contract.Fields.Notes),
new("created_at", x => x.Contract.Created.At),
new("updated_at", x => x.Contract.Updated.At),
```

Use a private record `Row(Contract Contract, ProjectSummary? Project, string? ContractorName)` instead of a tuple if that reads better. `Title => "Contracts"`. `Description => "Every contract in every project, with contractor, form, status, value and dates."`

- [ ] **Step 4: `units` dataset**

`UnitsExport(UnitStore store, IPortfolioQueries portfolio, IOptions<LocaleOptions> locale)`:
- `store.ListAllAsync(ct)` (it's GSI-backed and eventually consistent, which is fine for an export).
- Order by project code, then `u.Fields.Identifier` with `UnitIdentifiers.Order`.
- `currency` is `u.Currency ?? locale.Value.Currency`.

```csharp
new("unit_id", x => x.Unit.Id.Value),
new("project_id", x => x.Unit.ProjectId.Value),
new("project_code", x => x.Project?.Code),
new("project_name", x => x.Project?.Name),
new("identifier", x => x.Unit.Fields.Identifier),
new("type", x => x.Unit.Fields.Type),
new("bedrooms", x => x.Unit.Fields.Bedrooms),
new("bathrooms", x => x.Unit.Fields.Bathrooms),
new("car_parks", x => x.Unit.Fields.CarParks),
new("floor_area_sqm", x => x.Unit.Fields.FloorAreaSqm),
new("land_area_sqm", x => x.Unit.Fields.LandAreaSqm),
new("list_price", x => x.Unit.Fields.ListPrice?.Amount),
new("sale_status", x => x.Unit.Status),
new("buyer_name", x => x.Unit.Sale?.BuyerName),
new("buyer_solicitor", x => x.Unit.Sale?.BuyerSolicitor),
new("agreement_date", x => x.Unit.Sale?.AgreementDate),
new("sale_price", x => x.Unit.Sale?.SalePrice?.Amount),
new("deposit", x => x.Unit.Sale?.Deposit?.Amount),
new("unconditional_date", x => x.Unit.Sale?.UnconditionalDate),
new("settlement_date", x => x.Unit.Sale?.SettlementDate),
new("currency", x => x.Unit.Currency ?? currency),
new("notes", x => x.Unit.Fields.Notes),
new("created_at", x => x.Unit.Created.At),
new("updated_at", x => x.Unit.Updated.At),
```

Do **not** export `BuyerEmail` or `BuyerPhone`. `Title => "Units and sales"`. `Description => "Every unit or lot with its listing details and current sale: status, buyer, price and key dates."`

- [ ] **Step 5: Verify and commit**

1. Run the format and build steps, then `gen:api` (expect no OpenAPI change).
2. Manual check:
   - Create a risk whose mitigation has two lines and a `"quote"`, a contract with a value, and a unit with a Settled sale.
   - Download `risks`, `contracts` and `units`. Each has the spec's header row. The multi-line mitigation is one quoted field. Score and band match the UI. Contractor names are filled in. The units file has no buyer email or phone columns.
   - An archived project's risks still appear.
   - Catalog order is by title.

```bash
git commit -m "feat(reporting): risks, contracts and units datasets

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: `cost-lines`, `cost-entries`, `consents` and `consent-conditions` datasets

**Files:**
- Modify: `src/Modules/PDS.Costs/Data/CostStore.cs` (add `ScanAllAsync`), `PDS.Costs.csproj`, `CostsModule.cs`
- Create: `src/Modules/PDS.Costs/Features/CostLinesExport.cs`, `src/Modules/PDS.Costs/Features/CostEntriesExport.cs`
- Modify: `src/Modules/PDS.Consents/PDS.Consents.csproj`, `ConsentsModule.cs`
- Create: `src/Modules/PDS.Consents/Features/ConsentsExport.cs`, `src/Modules/PDS.Consents/Features/ConsentConditionsExport.cs`

**Interfaces:**
- Consumes: Task 1 contracts; `CostCalculator.For(lines, commitments, actuals) → (IReadOnlyDictionary<BudgetLineId, CostFigures>, CostFigures)`; `IContractQueries.GetProjectContracts(ProjectId, ct)`; `ConsentStore.ScanAllAsync(ct) → IReadOnlyList<ConsentWithConditions>`; `ConsentRules.ConditionState(Condition, bool consentActive, DateOnly today)`; `LocalDate.Today(TimeProvider, LocaleOptions)`
- Produces: datasets `cost-lines`, `cost-entries`, `consents`, `consent-conditions`

Each module gets the Reporting `ProjectReference` (with the comment) and two `AddSingleton<IExportDataset, …>()` lines.

- [ ] **Step 1: `CostStore.ScanAllAsync`**

```csharp
/// <summary>Every cost item in every project, grouped by project (full scan; for exports).</summary>
public async Task<IReadOnlyDictionary<ProjectId, ProjectCostData>> ScanAllAsync(CancellationToken ct)
```

- A paged `Scan` with `ConsistentRead = true`.
- Dispatch on the `sk` prefix exactly as `LoadProjectAsync` does: lines, commitments and actuals into per-project lists, keyed by the domain object's `ProjectId`.
- Return a `ProjectCostData` per project.

Factor the prefix-dispatch into a private helper used by both methods, rather than duplicating it.

- [ ] **Step 2: `cost-lines`**

`CostLinesExport(CostStore store, IPortfolioQueries portfolio, IOptions<LocaleOptions> locale)`:
- For each project's data, compute `var (figures, _) = CostCalculator.For(data.Lines, data.Commitments, data.Actuals);`.
- Emit one row per line.
- Order by project code, then `SortOrder`, then name.

```csharp
new("budget_line_id", x => x.Line.Id.Value),
new("project_id", x => x.Line.ProjectId.Value),
new("project_code", x => x.Project?.Code),
new("project_name", x => x.Project?.Name),
new("line_name", x => x.Line.Fields.Name),
new("sort_order", x => x.Line.Fields.SortOrder),
new("budget", x => x.Figures.Budget),
new("committed", x => x.Figures.Committed),
new("actual", x => x.Figures.Actual),
new("forecast_remaining", x => x.Line.Fields.ForecastRemaining?.Amount),
new("forecast_final_cost", x => x.Figures.ForecastFinalCost),
new("forecast_to_complete", x => x.Figures.ForecastToComplete),
new("variance", x => x.Figures.Variance),
new("currency", x => x.Line.Fields.Budget.Currency),
new("notes", x => x.Line.Fields.Notes),
```

`Title => "Cost report lines"`. `Description => "Every budget line in every project with budget, committed, actual, forecast final cost and variance (excl. GST)."`

- [ ] **Step 3: `cost-entries`**

`CostEntriesExport(CostStore store, IPortfolioQueries portfolio, IContractQueries contracts)`:
- For each project in the scan, call `contracts.GetProjectContracts(projectId, ct)` once, and build a line-name dictionary from `data.Lines`.
- Map commitments and actuals to one private row record:

```csharp
private sealed record Entry(
    Guid EntryId, string EntryKind, ProjectId ProjectId, ProjectSummary? Project, Guid BudgetLineId, string BudgetLineName,
    string Type, Guid? ContractId, string? ContractTitle, string? ContractorName, string? Payee,
    string Description, string? Reference, DateOnly Date, decimal Amount, string Currency, string? Notes, DateTimeOffset CreatedAt);
```

- `EntryKind` is `"Commitment"` or `"Actual"`. `Type` is the type enum's `ToString()`. `Payee` is null for commitments.
- A missing line gets `"(deleted line)"`. A `ContractId` not found among the project's contracts gets `ContractTitle = "(unknown contract)"` and `ContractorName = null`. With no contract link, all three contract fields are null. These are the same constants and logic as `GetCostReport`; copy them, and don't make `GetCostReport`'s private constants shared.
- Order by project code, then `Date`, then `EntryKind`.
- Columns, in spec order: `entry_id, entry_kind, project_id, project_code, project_name, budget_line_id, budget_line_name, type, contract_id, contract_title, contractor_name, payee, description, reference, date, amount, currency, notes, created_at`.

`Title => "Commitments and actuals"`. `Description => "Every commitment and actual cost in every project, with its budget line and contract (excl. GST)."`

- [ ] **Step 4: `consents` and `consent-conditions`**

`ConsentsExport(ConsentStore store, IPortfolioQueries portfolio)`:
- One row per `ConsentWithConditions`.
- Order by project code, then title.
- Columns: `consent_id, project_id, project_code, project_name, type, title, authority, reference, status, lodged_date, granted_date, expiry_date, condition_count (Conditions.Count), open_condition_count (Conditions.Count(c => c.CompletedOn is null)), notes, created_at, updated_at`.
- `Title => "Consents"`. `Description => "Every consent in every project with its authority, status, key dates and condition counts."`

`ConsentConditionsExport(ConsentStore store, IPortfolioQueries portfolio, TimeProvider clock, IOptions<LocaleOptions> locale)`:
- `today = LocalDate.Today(clock, locale.Value)`.
- One row per condition, carrying its consent.
- `due_state = ConsentRules.ConditionState(condition, consent.IsActive, today)`.
- Order by project code, consent title, due date (nulls last), then reference.
- Columns: `condition_id, consent_id, project_id, project_code, project_name, consent_type, consent_title, consent_reference, consent_status, reference, description, due_date, completed_on, due_state, created_at, updated_at`.
- `Title => "Consent conditions"`. `Description => "Every consent condition with its due date, completion and due state as of today."`

- [ ] **Step 5: Verify and commit**

1. Run the format, build and `gen:api` steps.
2. Manual check:
   - On a project, add standard lines, a commitment linked to a contract, a negative credit actual, and an actual on a line that you then delete (if deletion is blocked while the line has entries, skip that case and note it).
   - Compare `cost-lines` figures with `GET /api/costs/projects/{id}` line figures: they must match exactly.
   - The negative amount is a plain `-1234.5` with no `'` prefix.
   - `consent-conditions` `due_state` matches the due list for an overdue condition.

```bash
git commit -m "feat(reporting): cost and consent datasets

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Web — Reports page

**Files:**
- Create: `web/src/features/reporting/api.ts`, `web/src/features/reporting/ReportsPage.tsx`
- Modify (one line each): `web/src/routes.tsx` (import + `{ path: 'reports', element: <ReportsPage /> }`), `web/src/shared/layout/AppLayout.tsx` (a `NavLink` "Reports" to `/reports` after Sales)

**Interfaces:**
- Consumes: the `ListExports` and `DownloadExport` OpenAPI operations (Task 1); `useApi`, `unwrap`, `ApiError` from `web/src/shared/api/client`; `useMe` from `web/src/shared/me`

- [ ] **Step 1: api.ts**

```ts
import { useQuery } from '@tanstack/react-query';
import { ApiError, unwrap, useApi, type Api, type ProblemDetails } from '../../shared/api/client';
import type { paths } from '../../shared/api/schema';
import type { DeepRequired } from '../../shared/api/types';

export type ExportDataset = DeepRequired<
  paths['/api/reporting/exports']['get']['responses'][200]['content']['application/json']
>[number];

export function useExports(enabled: boolean) {
  const api = useApi();
  return useQuery({
    queryKey: ['reporting', 'exports'],
    queryFn: () => unwrap<ExportDataset[]>(api.GET('/api/reporting/exports')),
    enabled,
  });
}

function today(): string {
  const d = new Date();
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

/** Fetches the CSV with the bearer token and saves it through a temporary link. */
export async function downloadExport(api: Api, name: string): Promise<void> {
  const { data, error, response } = await api.GET('/api/reporting/exports/{name}', {
    params: { path: { name } },
    parseAs: 'blob',
  });
  if (!response.ok || !data) throw new ApiError(response.status, (error ?? undefined) as ProblemDetails | undefined);
  const url = URL.createObjectURL(data as Blob);
  try {
    const a = document.createElement('a');
    a.href = url;
    a.download = `pds-${name}-${today()}.csv`;
    document.body.appendChild(a);
    a.click();
    a.remove();
  } finally {
    setTimeout(() => URL.revokeObjectURL(url), 0);
  }
}
```

Adjust the response type path to whatever `schema.d.ts` generated. With `parseAs: 'blob'`, openapi-fetch returns a `Blob` regardless of the declared schema. If the error body isn't parsed when using blob mode, construct the `ApiError` from the status alone.

- [ ] **Step 2: ReportsPage.tsx**

- `const me = useMe(); const canExport = !!me.data && (me.data.roles.includes('Manager') || me.data.roles.includes('Admin'));`
- `const exports = useExports(canExport);`
- Render a `Stack`:
  - `Title order={2}` "Reports"
  - A `Text c="dimmed"`: "Each download is a CSV file covering every project, including archived ones. It opens in Excel. Amounts are in the deployment currency and exclude GST."
- While `me` is loading, show a `Loader`. If `!canExport`, show `<Alert color="blue" title="Exports">Exports are available to Managers and Admins.</Alert>`.
- Otherwise render a `Table` (columns Report, Contents and an empty actions column) with one row per dataset: title (`fw={500}`), description, and `<Button size="xs" variant="light" loading={busy === d.name} onClick={...}>Download CSV</Button>`.
  - `busy` is `useState<string | null>`.
  - On click: `setBusy(name)`, `await downloadExport(api, name)`, catch → `notifications.show({ color: 'red', title: 'Download failed', message: err instanceof ApiError ? err.problem?.title ?? err.message : 'Something went wrong.' })`, finally `setBusy(null)`.
  - Check how other pages show errors (`web/src/shared/mutationError.ts`) and reuse that helper if it fits.
- Show the catalog's loading and error states like `ConsentsDuePage` does.

- [ ] **Step 3: Wiring, verify, commit**

1. Add the route and the nav link (one line each).
2. In `web/`, run `npm run gen:api && npm run typecheck && npm run lint && npm run build`.
3. Run `cdk synth` (`dotnet publish` to `artifacts/api` per the README first). Expect no diff in the stacks.
4. Manual check:
   - Run the API on 5088 and Vite on 5198 (untracked config).
   - As a Manager, open `/reports`: eight rows ordered by title. Download `cost-lines`: the file saves as `pds-cost-lines-<today>.csv`.
   - As a Viewer (dev roles), the page shows the info alert and the network tab shows no `/api/reporting` call.
5. Delete the untracked Vite config.

```bash
git commit -m "feat(web): reports page with CSV downloads

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
