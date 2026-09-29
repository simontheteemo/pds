# PDS Foundation + Project Portfolio Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the deployable foundation of the property development system (PDS), with the Project Portfolio module as its first vertical slice: API, DynamoDB storage, React UI, AWS infrastructure and CI/CD.

**Architecture:** A .NET 10 modular monolith (ASP.NET Core Minimal APIs) runs on AWS Lambda behind an API Gateway HTTP API with a Cognito JWT authorizer. Each module is one C# project. Only its `Contracts/` namespace and its `*Module` registration class are public, and each module owns one DynamoDB table accessed through the low-level AWS SDK client. A React SPA is served from S3 + CloudFront, and everything is defined with AWS CDK in C#.

**Tech Stack:** .NET 10 / C# 14, ASP.NET Core Minimal APIs, AWSSDK.DynamoDBv2 4.x, FluentValidation 12, Serilog, xUnit 2 + Testcontainers (DynamoDB Local), React 19 + Vite 8 + TypeScript 5.9 + Mantine 9 + TanStack Query 5 + React Router 8 + Zod 4, Vitest 5 + MSW 3, AWS CDK 2 (C#), GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-09-29-foundation-design.md` (Rev B). Also read `docs/adr/0001-dotnet-10-lts.md`, `docs/adr/0002-dynamodb.md` and `docs/tech-stack.md`.

## Global Constraints

- Target framework `net10.0`, `Nullable` enabled, `TreatWarningsAsErrors` true for every C# project except `infra/PDS.Infra` (jsii-generated CDK types emit deprecation warnings).
- Package versions live **only** in `Directory.Packages.props` (central package management). No `Version=` attribute in any `.csproj`.
- No EF Core, ORM, MediatR, AutoMapper, message bus or repository abstraction. Handlers use the module's store class directly.
- A module is one project. Everything in it is `internal` except the `PDS.<Module>.Contracts` namespace and the `<Module>Module` static class.
- Every DynamoDB table uses string keys `pk` / `sk`; list indexes are named `gsi1` with keys `gsi1pk` / `gsi1sk`, projection ALL, on-demand billing.
- Physical table name = config `Tables:<logical>`, falling back to `pds-<logical>`.
- No client-specific names, logos or rules in source code. Branding and locale come from configuration (defaults: product name "Property Development System", primary colour "teal", currency NZD, culture en-NZ, time zone Pacific/Auckland).
- Roles are exactly `Admin`, `Manager`, `Viewer`. Policies: `CanRead` = any role, `CanWrite` = Manager or Admin, `CanAdminister` = Admin.
- JSON is camelCase. Enums are serialised as strings, and integer enum values are rejected. Errors are `application/problem+json`, and validation errors are keyed by camelCase dotted paths (`site.city`).
- Project code: trimmed and upper-cased, pattern `^[A-Z0-9][A-Z0-9-]{0,19}$`, unique per deployment.
- List paging: `page` ≥ 1, `pageSize` clamped to 1–100, default 25; results ordered by code (ordinal).
- The UI hides actions for convenience only; the server enforces every permission.
- Every commit message ends with the trailer `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

These are inputs the spec implies but doesn't spell out, ranked by how likely they are to hurt a real user. Each has a pinned test in the task that owns the code.

1. **Cognito group claims in unexpected shapes.** API Gateway passes `cognito:groups` as one bracketed string (`"[Admin Manager]"`), and a group may be created in lower case. Users must still get their roles instead of a 403 (Task 3, `CognitoGroupsClaimsTransformationTests`).
2. **Omitted or numeric enum values.** A body with no `stage`, or `"stage": 2`, must return a 400 on `stage`, not silently save as `Acquisition` (Task 5, `Create_rejects_missing_or_numeric_stage`).
3. **Project codes differing only in case or whitespace.** `" pds-001 "` and `"PDS-001"` are the same code; the second create must be a 400 on `code` (Task 5, `Duplicate_code_ignoring_case_and_whitespace_is_rejected`).
4. **Out-of-range paging.** `pageSize=0`, `pageSize=1000`, `page=0` and `page=2147483647` must clamp or return an empty page with the right total, never throw or return everything (Task 6, `Paging_is_clamped_and_never_overflows`).
5. **Date-only values shown in NZ time.** `2026-03-01` must render as 1 Mar 2026, not 28 Feb (Task 11, `format.test.ts`).

## Before you start (human prerequisites)

- [ ] Install the **.NET 10 SDK (10.0.401 or later 10.0.x)** from https://dotnet.microsoft.com/download/dotnet/10.0 (macOS installer; needs admin). Verify: `dotnet --list-sdks` shows a `10.0.` line.
- [ ] Docker Desktop is running (`docker info` succeeds). Testcontainers and local dev need it.
- [ ] Node.js ≥ 22.12 is on PATH (`node -v`). Node 24 LTS is recommended.
- [ ] Work happens on a feature branch: `git checkout -b feat/foundation`.

> **Library versions note:** several frontend packages (Vite 8, React Router 8, Mantine 9, Zod 4, MSW 3, Vitest 5) are newer than some reference material. Their exports were checked when this plan was written. If an API in a code block has changed, follow the package's migration guide but keep the behaviour and the tests exactly as written.

## File Map

```
pds/
├── global.json · .gitignore · .editorconfig · Directory.Build.props · Directory.Packages.props · PDS.slnx
├── docker-compose.yml · README.md
├── src/
│   ├── PDS.Shared/
│   │   ├── Money.cs · PagedResult.cs
│   │   ├── Data/ Attr.cs · TableDefinition.cs · TableNames.cs · TableBootstrapper.cs · DynamoServiceCollectionExtensions.cs
│   │   ├── Security/ Roles.cs · ICurrentUser.cs
│   │   ├── Settings/ LocaleOptions.cs
│   │   └── Web/ ValidationFilter.cs · Problems.cs
│   ├── PDS.Api/
│   │   ├── Program.cs · HostEndpoints.cs · CorrelationIdMiddleware.cs · Settings.cs
│   │   ├── Security/ AuthOptions.cs · SecurityServiceCollectionExtensions.cs · GatewayAuthenticationHandler.cs
│   │   │             DevelopmentAuthenticationHandler.cs · CognitoGroupsClaimsTransformation.cs · HttpCurrentUser.cs
│   │   ├── appsettings.json · appsettings.Development.json · Properties/launchSettings.json
│   └── Modules/PDS.Portfolio/
│       ├── PortfolioModule.cs
│       ├── Contracts/ ProjectId.cs · ProjectSummary.cs · IPortfolioQueries.cs
│       ├── Domain/ Project.cs · ProjectFields.cs · Site.cs · ProjectStage.cs · ProjectStatus.cs · ProjectRules.cs · AuditStamp.cs
│       ├── Data/ PortfolioTable.cs · ProjectItemMapper.cs · ProjectStore.cs · SaveResult.cs · PortfolioQueries.cs
│       └── Features/ ProjectDtos.cs · ProjectInputValidator.cs · PortfolioProblems.cs
│                     CreateProject.cs · GetProject.cs · ListProjects.cs · UpdateProject.cs · ArchiveProject.cs
├── tests/
│   ├── PDS.Tests/ (unit + architecture)   ├── PDS.IntegrationTests/ (HTTP + DynamoDB Local)   └── PDS.Infra.Tests/
├── web/ (Vite React SPA; see Tasks 11–14)
├── infra/ cdk.json · deployments/example-dev.json · PDS.Infra/ (Program.cs, DeploymentSettings.cs, DataStack.cs, AuthStack.cs, AppStack.cs, GitHubOidcStack.cs)
└── .github/workflows/ ci.yml · deploy.yml · deploy-environment.yml
```

---

### Task 1: Repository skeleton and shared kernel

**Files:**
- Create: `global.json`, `.gitignore`, `.editorconfig`, `Directory.Build.props`, `Directory.Packages.props`, `PDS.slnx`
- Create: `src/PDS.Shared/PDS.Shared.csproj`, `src/PDS.Shared/Money.cs`, `src/PDS.Shared/PagedResult.cs`
- Create: `src/PDS.Shared/Data/Attr.cs`, `Data/TableDefinition.cs`, `Data/TableNames.cs`, `Data/TableBootstrapper.cs`, `Data/DynamoServiceCollectionExtensions.cs`
- Create: `src/PDS.Shared/Security/Roles.cs`, `Security/ICurrentUser.cs`, `src/PDS.Shared/Settings/LocaleOptions.cs`
- Create: `src/PDS.Shared/Web/ValidationFilter.cs`, `Web/Problems.cs`
- Test: `tests/PDS.Tests/PDS.Tests.csproj`, `tests/PDS.Tests/Shared/MoneyTests.cs`, `Shared/AttrTests.cs`, `Shared/ValidationErrorsTests.cs`, `Shared/TableBootstrapperTests.cs`, `Shared/TableNamesTests.cs`

**Interfaces:**
- Produces (namespace `PDS.Shared`): `sealed record Money(decimal Amount, string Currency)`, `sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)`.
- Produces (`PDS.Shared.Data`): static `Attr` (`S`, `N(decimal)`, `N(long)`, `Bool`, `Map`, `Date`, `Timestamp`, `SetIfPresent(...)`, `GetString`, `GetStringOrNull`, `GetDecimalOrNull`, `GetLong`, `GetBool`, `GetDateOrNull`, `GetTimestamp`, `GetMap`); `TableKeys.PartitionKey = "pk"`, `TableKeys.SortKey = "sk"`; `sealed record GlobalIndex(string Name, string PartitionKey, string SortKey)`; `sealed record TableDefinition(string LogicalName, IReadOnlyList<GlobalIndex> GlobalIndexes)`; `sealed class TableNames { string For(string logicalName) }`; `sealed class TableBootstrapper { Task EnsureTablesAsync(CancellationToken); static CreateTableRequest BuildCreateRequest(string, TableDefinition) }`; `IServiceCollection AddDynamo(this IServiceCollection)`.
- Produces (`PDS.Shared.Security`): `Roles.Admin/Manager/Viewer`, `Roles.All`, `Policies.CanRead/CanWrite/CanAdminister`, `interface ICurrentUser { string Id; string Name; string? Email; IReadOnlyList<string> Roles; }`.
- Produces (`PDS.Shared.Settings`): `sealed class LocaleOptions { const string Section = "Locale"; string Currency; string Culture; string TimeZone; }`.
- Produces (`PDS.Shared.Web`): `ValidationFilter<T>`, `RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder)`, `ValidationErrors.ToCamelCasePath(string)`, `ValidationErrors.ToDictionary(ValidationResult)`, `Problems.Field(string field, string message) → ValidationProblem`, `Problems.Conflict(string title, string detail) → ProblemHttpResult`.

- [ ] **Step 1: Create repository configuration files**

`global.json`:
```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

`.gitignore`:
```
bin/
obj/
artifacts/
TestResults/
*.user
.vs/
.idea/
.DS_Store
node_modules/
web/dist/
coverage/
infra/cdk.out/
infra/cdk-outputs.json
```

`.editorconfig`:
```ini
root = true

[*]
indent_style = space
indent_size = 4
end_of_line = lf
charset = utf-8
insert_final_newline = true
trim_trailing_whitespace = true

[*.{json,yml,yaml,ts,tsx,js,cjs,mjs,css,html,md}]
indent_size = 2

[*.cs]
csharp_style_namespace_declarations = file_scoped:warning
dotnet_sort_system_directives_first = true
```

`Directory.Build.props`:
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest</AnalysisLevel>
  </PropertyGroup>
</Project>
```

`Directory.Packages.props`:
```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="AWSSDK.DynamoDBv2" Version="4.0.106" />
    <PackageVersion Include="Amazon.Lambda.AspNetCoreServer.Hosting" Version="2.2.1" />
    <PackageVersion Include="Amazon.CDK.Lib" Version="2.271.0" />
    <PackageVersion Include="Constructs" Version="10.8.1" />
    <PackageVersion Include="FluentValidation" Version="12.1.1" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="12.1.1" />
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.12" />
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.12" />
    <PackageVersion Include="Microsoft.Extensions.ApiDescription.Server" Version="10.0.12" />
    <PackageVersion Include="Microsoft.Extensions.TimeProvider.Testing" Version="10.10.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.10.1" />
    <PackageVersion Include="Serilog.AspNetCore" Version="10.0.0" />
    <PackageVersion Include="Serilog.Formatting.Compact" Version="3.0.0" />
    <PackageVersion Include="Testcontainers.DynamoDb" Version="4.15.0" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
  </ItemGroup>
</Project>
```

Run: `dotnet new sln --name PDS --format slnx`
Expected: `PDS.slnx` created.

- [ ] **Step 2: Create the Shared project and the unit test project**

`src/PDS.Shared/PDS.Shared.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="AWSSDK.DynamoDBv2" />
    <PackageReference Include="FluentValidation" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="PDS.Tests" />
  </ItemGroup>
</Project>
```

`tests/PDS.Tests/PDS.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" PrivateAssets="all" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/PDS.Shared/PDS.Shared.csproj" />
  </ItemGroup>
</Project>
```

Run: `dotnet sln PDS.slnx add src/PDS.Shared/PDS.Shared.csproj tests/PDS.Tests/PDS.Tests.csproj`

- [ ] **Step 3: Write the failing tests**

`tests/PDS.Tests/Shared/MoneyTests.cs`:
```csharp
using PDS.Shared;

namespace PDS.Tests.Shared;

public class MoneyTests
{
    [Fact]
    public void Accepts_amounts_with_two_decimal_places()
    {
        var money = new Money(1234.56m, "NZD");

        Assert.Equal(1234.56m, money.Amount);
        Assert.Equal("NZD", money.Currency);
    }

    [Fact]
    public void Rejects_more_than_two_decimal_places() =>
        Assert.Throws<ArgumentException>(() => new Money(1.005m, "NZD"));

    [Theory]
    [InlineData("nzd")]
    [InlineData("NZ")]
    [InlineData("NZDD")]
    [InlineData("")]
    public void Rejects_invalid_currency_codes(string currency) =>
        Assert.Throws<ArgumentException>(() => new Money(1m, currency));

    [Fact]
    public void Amounts_with_different_scale_are_equal() =>
        Assert.Equal(new Money(1.1m, "NZD"), new Money(1.10m, "NZD"));
}
```

`tests/PDS.Tests/Shared/AttrTests.cs`:
```csharp
using Amazon.DynamoDBv2.Model;
using PDS.Shared.Data;

namespace PDS.Tests.Shared;

public class AttrTests
{
    [Fact]
    public void Decimal_round_trips_through_number_attribute()
    {
        var item = new Dictionary<string, AttributeValue> { ["amount"] = Attr.N(12500000.50m) };

        Assert.Equal(12500000.50m, Attr.GetDecimalOrNull(item, "amount"));
    }

    [Fact]
    public void Date_round_trips_as_iso_date_string()
    {
        var item = new Dictionary<string, AttributeValue> { ["d"] = Attr.Date(new DateOnly(2026, 3, 1)) };

        Assert.Equal("2026-03-01", item["d"].S);
        Assert.Equal(new DateOnly(2026, 3, 1), Attr.GetDateOrNull(item, "d"));
    }

    [Fact]
    public void Timestamp_round_trips_in_utc()
    {
        var at = new DateTimeOffset(2026, 9, 29, 10, 15, 30, TimeSpan.FromHours(13));
        var item = new Dictionary<string, AttributeValue> { ["t"] = Attr.Timestamp(at) };

        var read = Attr.GetTimestamp(item, "t");

        Assert.Equal(at, read);
        Assert.Equal(TimeSpan.Zero, read.Offset);
    }

    [Fact]
    public void Missing_optional_attributes_read_as_null_or_false()
    {
        var item = new Dictionary<string, AttributeValue>();

        Assert.Null(Attr.GetStringOrNull(item, "x"));
        Assert.Null(Attr.GetDecimalOrNull(item, "x"));
        Assert.Null(Attr.GetDateOrNull(item, "x"));
        Assert.False(Attr.GetBool(item, "x"));
    }

    [Fact]
    public void Missing_required_attribute_throws_invalid_data()
    {
        var item = new Dictionary<string, AttributeValue>();

        Assert.Throws<InvalidDataException>(() => Attr.GetString(item, "code"));
        Assert.Throws<InvalidDataException>(() => Attr.GetLong(item, "version"));
    }

    [Fact]
    public void SetIfPresent_skips_null_and_empty_values()
    {
        var item = new Dictionary<string, AttributeValue>();

        Attr.SetIfPresent(item, "a", (string?)null);
        Attr.SetIfPresent(item, "b", "");
        Attr.SetIfPresent(item, "c", (decimal?)null);
        Attr.SetIfPresent(item, "d", (DateOnly?)null);
        Attr.SetIfPresent(item, "e", "kept");

        Assert.Equal(new[] { "e" }, item.Keys);
    }
}
```

`tests/PDS.Tests/Shared/ValidationErrorsTests.cs`:
```csharp
using FluentValidation.Results;
using PDS.Shared.Web;

namespace PDS.Tests.Shared;

public class ValidationErrorsTests
{
    [Theory]
    [InlineData("Code", "code")]
    [InlineData("Site.City", "site.city")]
    [InlineData("PlannedCompletion", "plannedCompletion")]
    [InlineData("", "")]
    public void Converts_property_paths_to_camel_case(string input, string expected) =>
        Assert.Equal(expected, ValidationErrors.ToCamelCasePath(input));

    [Fact]
    public void Groups_messages_by_camel_case_path()
    {
        var result = new ValidationResult([
            new ValidationFailure("Site.City", "City is required."),
            new ValidationFailure("Site.City", "City is required."),
            new ValidationFailure("Name", "Name is required."),
        ]);

        var errors = ValidationErrors.ToDictionary(result);

        Assert.Equal(new[] { "City is required." }, errors["site.city"]);
        Assert.Equal(new[] { "Name is required." }, errors["name"]);
    }
}
```

`tests/PDS.Tests/Shared/TableBootstrapperTests.cs`:
```csharp
using Amazon.DynamoDBv2;
using PDS.Shared.Data;

namespace PDS.Tests.Shared;

public class TableBootstrapperTests
{
    [Fact]
    public void Builds_on_demand_table_with_pk_sk_and_gsi()
    {
        var definition = new TableDefinition("portfolio", [new GlobalIndex("gsi1", "gsi1pk", "gsi1sk")]);

        var request = TableBootstrapper.BuildCreateRequest("pds-portfolio", definition);

        Assert.Equal("pds-portfolio", request.TableName);
        Assert.Equal(BillingMode.PAY_PER_REQUEST, request.BillingMode);
        Assert.Equal(new[] { "pk", "sk", "gsi1pk", "gsi1sk" }, request.AttributeDefinitions.Select(a => a.AttributeName));
        Assert.All(request.AttributeDefinitions, a => Assert.Equal(ScalarAttributeType.S, a.AttributeType));
        var index = Assert.Single(request.GlobalSecondaryIndexes);
        Assert.Equal("gsi1", index.IndexName);
        Assert.Equal(ProjectionType.ALL, index.Projection.ProjectionType);
    }

    [Fact]
    public void Omits_index_list_when_table_has_no_indexes()
    {
        var request = TableBootstrapper.BuildCreateRequest("t", new TableDefinition("t", []));

        Assert.Null(request.GlobalSecondaryIndexes);
    }
}
```

`tests/PDS.Tests/Shared/TableNamesTests.cs`:
```csharp
using Microsoft.Extensions.Configuration;
using PDS.Shared.Data;

namespace PDS.Tests.Shared;

public class TableNamesTests
{
    [Fact]
    public void Uses_configured_name_case_insensitively()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Tables:Portfolio"] = "seine-dev-portfolio" })
            .Build();

        Assert.Equal("seine-dev-portfolio", new TableNames(config).For("portfolio"));
    }

    [Fact]
    public void Falls_back_to_pds_prefix() =>
        Assert.Equal("pds-portfolio", new TableNames(new ConfigurationBuilder().Build()).For("portfolio"));
}
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `dotnet test tests/PDS.Tests`
Expected: build FAILS with errors such as `The type or namespace name 'Money' could not be found`.

- [ ] **Step 5: Implement the shared kernel**

`src/PDS.Shared/Money.cs`:
```csharp
namespace PDS.Shared;

public sealed record Money
{
    public Money(decimal amount, string currency)
    {
        if (decimal.Round(amount, 2) != amount)
            throw new ArgumentException("Amount cannot have more than 2 decimal places.", nameof(amount));
        if (currency is not { Length: 3 } || !currency.All(char.IsAsciiLetterUpper))
            throw new ArgumentException("Currency must be a 3-letter upper-case ISO 4217 code.", nameof(currency));

        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }
}
```

`src/PDS.Shared/PagedResult.cs`:
```csharp
namespace PDS.Shared;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
```

`src/PDS.Shared/Data/Attr.cs`:
```csharp
using System.Globalization;
using Amazon.DynamoDBv2.Model;

namespace PDS.Shared.Data;

/// <summary>Reads and writes DynamoDB attribute values. Missing optional attributes read as null (or false).</summary>
public static class Attr
{
    private const string DateFormat = "yyyy-MM-dd";

    public static AttributeValue S(string value) => new() { S = value };

    public static AttributeValue N(decimal value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };

    public static AttributeValue N(long value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };

    public static AttributeValue Bool(bool value) => new() { BOOL = value };

    public static AttributeValue Map(Dictionary<string, AttributeValue> value) => new() { M = value };

    public static AttributeValue Date(DateOnly value) => S(value.ToString(DateFormat, CultureInfo.InvariantCulture));

    public static AttributeValue Timestamp(DateTimeOffset value) =>
        S(value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

    public static void SetIfPresent(Dictionary<string, AttributeValue> item, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            item[key] = S(value);
    }

    public static void SetIfPresent(Dictionary<string, AttributeValue> item, string key, decimal? value)
    {
        if (value is { } v)
            item[key] = N(v);
    }

    public static void SetIfPresent(Dictionary<string, AttributeValue> item, string key, DateOnly? value)
    {
        if (value is { } v)
            item[key] = Date(v);
    }

    public static string GetString(IReadOnlyDictionary<string, AttributeValue> item, string key) =>
        GetStringOrNull(item, key) ?? throw Missing(key);

    public static string? GetStringOrNull(IReadOnlyDictionary<string, AttributeValue> item, string key) =>
        item.TryGetValue(key, out var value) ? value.S : null;

    public static decimal? GetDecimalOrNull(IReadOnlyDictionary<string, AttributeValue> item, string key) =>
        item.TryGetValue(key, out var value) && value.N is { } n
            ? decimal.Parse(n, NumberStyles.Float, CultureInfo.InvariantCulture)
            : null;

    public static long GetLong(IReadOnlyDictionary<string, AttributeValue> item, string key) =>
        item.TryGetValue(key, out var value) && value.N is { } n
            ? long.Parse(n, NumberStyles.Integer, CultureInfo.InvariantCulture)
            : throw Missing(key);

    public static bool GetBool(IReadOnlyDictionary<string, AttributeValue> item, string key) =>
        item.TryGetValue(key, out var value) && value.BOOL == true;

    public static DateOnly? GetDateOrNull(IReadOnlyDictionary<string, AttributeValue> item, string key) =>
        GetStringOrNull(item, key) is { } s ? DateOnly.ParseExact(s, DateFormat, CultureInfo.InvariantCulture) : null;

    public static DateTimeOffset GetTimestamp(IReadOnlyDictionary<string, AttributeValue> item, string key) =>
        DateTimeOffset.Parse(GetString(item, key), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public static IReadOnlyDictionary<string, AttributeValue> GetMap(IReadOnlyDictionary<string, AttributeValue> item, string key) =>
        item.TryGetValue(key, out var value) && value.M is { } map ? map : throw Missing(key);

    private static InvalidDataException Missing(string key) => new($"Item is missing required attribute '{key}'.");
}
```

`src/PDS.Shared/Data/TableDefinition.cs`:
```csharp
namespace PDS.Shared.Data;

public static class TableKeys
{
    public const string PartitionKey = "pk";
    public const string SortKey = "sk";
}

public sealed record GlobalIndex(string Name, string PartitionKey, string SortKey);

/// <summary>Key layout of one module table. Local dev and tests create tables from it; CDK creates the real ones.</summary>
public sealed record TableDefinition(string LogicalName, IReadOnlyList<GlobalIndex> GlobalIndexes);
```

`src/PDS.Shared/Data/TableNames.cs`:
```csharp
using Microsoft.Extensions.Configuration;

namespace PDS.Shared.Data;

public sealed class TableNames(IConfiguration configuration)
{
    public string For(string logicalName) =>
        configuration[$"Tables:{logicalName}"] is { Length: > 0 } name ? name : $"pds-{logicalName}";
}
```

`src/PDS.Shared/Data/TableBootstrapper.cs`:
```csharp
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace PDS.Shared.Data;

/// <summary>Creates missing module tables. Used only when Dynamo:CreateTablesOnStartup is true (local dev, tests).</summary>
public sealed class TableBootstrapper(IAmazonDynamoDB dynamo, TableNames names, IEnumerable<TableDefinition> definitions)
{
    public async Task EnsureTablesAsync(CancellationToken ct)
    {
        var existing = await ListTableNamesAsync(ct);
        foreach (var definition in definitions)
        {
            var name = names.For(definition.LogicalName);
            if (existing.Contains(name))
                continue;

            try
            {
                await dynamo.CreateTableAsync(BuildCreateRequest(name, definition), ct);
            }
            catch (ResourceInUseException)
            {
                // Created concurrently by another host; nothing to do.
            }
        }
    }

    public static CreateTableRequest BuildCreateRequest(string tableName, TableDefinition definition)
    {
        var keyNames = new[] { TableKeys.PartitionKey, TableKeys.SortKey }
            .Concat(definition.GlobalIndexes.SelectMany(i => new[] { i.PartitionKey, i.SortKey }))
            .Distinct();

        return new CreateTableRequest
        {
            TableName = tableName,
            BillingMode = BillingMode.PAY_PER_REQUEST,
            AttributeDefinitions = keyNames.Select(k => new AttributeDefinition(k, ScalarAttributeType.S)).ToList(),
            KeySchema =
            [
                new KeySchemaElement(TableKeys.PartitionKey, KeyType.HASH),
                new KeySchemaElement(TableKeys.SortKey, KeyType.RANGE),
            ],
            GlobalSecondaryIndexes = definition.GlobalIndexes.Count == 0
                ? null
                : definition.GlobalIndexes.Select(i => new GlobalSecondaryIndex
                {
                    IndexName = i.Name,
                    KeySchema =
                    [
                        new KeySchemaElement(i.PartitionKey, KeyType.HASH),
                        new KeySchemaElement(i.SortKey, KeyType.RANGE),
                    ],
                    Projection = new Projection { ProjectionType = ProjectionType.ALL },
                }).ToList(),
        };
    }

    private async Task<HashSet<string>> ListTableNamesAsync(CancellationToken ct)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        string? start = null;
        do
        {
            var response = await dynamo.ListTablesAsync(new ListTablesRequest { ExclusiveStartTableName = start }, ct);
            names.UnionWith(response.TableNames ?? []);
            start = response.LastEvaluatedTableName;
        }
        while (start is not null);

        return names;
    }
}

/// <summary>Runs the bootstrapper before the web server starts, so no request can reach a missing table.</summary>
internal sealed class TableBootstrapperHostedService(TableBootstrapper bootstrapper, IConfiguration configuration)
    : IHostedLifecycleService
{
    public Task StartingAsync(CancellationToken cancellationToken) =>
        configuration.GetValue<bool>("Dynamo:CreateTablesOnStartup")
            ? bootstrapper.EnsureTablesAsync(cancellationToken)
            : Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

`src/PDS.Shared/Data/DynamoServiceCollectionExtensions.cs`:
```csharp
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PDS.Shared.Data;

public static class DynamoServiceCollectionExtensions
{
    /// <summary>
    /// Registers the DynamoDB client. With Dynamo:ServiceUrl set (DynamoDB Local), it uses dummy credentials;
    /// otherwise it uses the default AWS credential chain and region (the Lambda role in AWS).
    /// </summary>
    public static IServiceCollection AddDynamo(this IServiceCollection services)
    {
        services.AddSingleton<IAmazonDynamoDB>(sp =>
        {
            var serviceUrl = sp.GetRequiredService<IConfiguration>()["Dynamo:ServiceUrl"];
            if (string.IsNullOrEmpty(serviceUrl))
                return new AmazonDynamoDBClient();

            return new AmazonDynamoDBClient(
                new BasicAWSCredentials("local", "local"),
                new AmazonDynamoDBConfig { ServiceURL = serviceUrl, AuthenticationRegion = "ap-southeast-2" });
        });
        services.AddSingleton<TableNames>();
        services.AddSingleton<TableBootstrapper>();
        services.AddHostedService<TableBootstrapperHostedService>();
        return services;
    }
}
```

`src/PDS.Shared/Security/Roles.cs`:
```csharp
namespace PDS.Shared.Security;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Viewer = "Viewer";

    public static IReadOnlyList<string> All { get; } = [Admin, Manager, Viewer];
}

public static class Policies
{
    public const string CanRead = "CanRead";
    public const string CanWrite = "CanWrite";
    public const string CanAdminister = "CanAdminister";
}
```

`src/PDS.Shared/Security/ICurrentUser.cs`:
```csharp
namespace PDS.Shared.Security;

public interface ICurrentUser
{
    string Id { get; }

    string Name { get; }

    string? Email { get; }

    IReadOnlyList<string> Roles { get; }
}
```

`src/PDS.Shared/Settings/LocaleOptions.cs`:
```csharp
namespace PDS.Shared.Settings;

public sealed class LocaleOptions
{
    public const string Section = "Locale";

    public string Currency { get; set; } = "NZD";

    public string Culture { get; set; } = "en-NZ";

    public string TimeZone { get; set; } = "Pacific/Auckland";
}
```

`src/PDS.Shared/Web/ValidationFilter.cs`:
```csharp
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace PDS.Shared.Web;

/// <summary>Runs the registered FluentValidation validator for the request argument of type T.</summary>
public sealed class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (validator is null || argument is null)
            return await next(context);

        var result = await validator.ValidateAsync(argument, context.HttpContext.RequestAborted);
        return result.IsValid
            ? await next(context)
            : TypedResults.ValidationProblem(ValidationErrors.ToDictionary(result));
    }
}

public static class ValidationErrors
{
    public static Dictionary<string, string[]> ToDictionary(ValidationResult result) =>
        result.Errors
            .GroupBy(e => ToCamelCasePath(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

    public static string ToCamelCasePath(string path) =>
        string.Join('.', path.Split('.').Select(s => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..]));
}

public static class EndpointValidationExtensions
{
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder) where T : class =>
        builder.AddEndpointFilter<ValidationFilter<T>>().ProducesValidationProblem();
}
```

`src/PDS.Shared/Web/Problems.cs`:
```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace PDS.Shared.Web;

public static class Problems
{
    public static ValidationProblem Field(string field, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    public static ProblemHttpResult Conflict(string title, string detail) =>
        TypedResults.Problem(title: title, detail: detail, statusCode: StatusCodes.Status409Conflict);
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/PDS.Tests`
Expected: PASS (all tests in `PDS.Tests.Shared`, 0 failed).

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: repository skeleton and shared kernel

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: API host, configuration endpoint and integration test harness

**Files:**
- Create: `src/PDS.Api/PDS.Api.csproj`, `src/PDS.Api/Program.cs`, `src/PDS.Api/Settings.cs`, `src/PDS.Api/HostEndpoints.cs`, `src/PDS.Api/CorrelationIdMiddleware.cs`, `src/PDS.Api/appsettings.json`
- Test: `tests/PDS.IntegrationTests/PDS.IntegrationTests.csproj`, `tests/PDS.IntegrationTests/ApiFactory.cs`, `tests/PDS.IntegrationTests/HostEndpointsTests.cs`

**Interfaces:**
- Consumes: `AddDynamo()`, `LocaleOptions` (Task 1).
- Produces: `public partial class Program` (test entry point); `BrandingOptions { ProductName, LogoUrl, PrimaryColor }` (section `Branding`); `AddPdsSettings(IServiceCollection, IConfiguration)`; `GET /api/config` returning `ClientConfig`; `CorrelationIdMiddleware.Header = "X-Correlation-Id"`; test fixture `ApiFactory` + `[Collection(ApiCollection.Name)]`.

- [ ] **Step 1: Create the API project and the integration test project**

`src/PDS.Api/PDS.Api.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <PackageReference Include="Amazon.Lambda.AspNetCoreServer.Hosting" />
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
    <PackageReference Include="Serilog.AspNetCore" />
    <PackageReference Include="Serilog.Formatting.Compact" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../PDS.Shared/PDS.Shared.csproj" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="PDS.Tests" />
  </ItemGroup>
</Project>
```

`tests/PDS.IntegrationTests/PDS.IntegrationTests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Testcontainers.DynamoDb" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" PrivateAssets="all" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/PDS.Api/PDS.Api.csproj" />
  </ItemGroup>
</Project>
```

Add a reference from the unit tests to the API (needed from Task 3): in `tests/PDS.Tests/PDS.Tests.csproj` add `<ProjectReference Include="../../src/PDS.Api/PDS.Api.csproj" />` to the existing `ProjectReference` item group.

Run: `dotnet sln PDS.slnx add src/PDS.Api/PDS.Api.csproj tests/PDS.IntegrationTests/PDS.IntegrationTests.csproj`

- [ ] **Step 2: Write the failing tests**

`tests/PDS.IntegrationTests/ApiFactory.cs`:
```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.DynamoDb;

namespace PDS.IntegrationTests;

/// <summary>One API host plus one DynamoDB Local container, shared by every test in the "api" collection.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly DynamoDbContainer _dynamo = new DynamoDbBuilder("amazon/dynamodb-local:3.3.1").Build();

    public async Task InitializeAsync() => await _dynamo.StartAsync();

    Task IAsyncLifetime.DisposeAsync() => _dynamo.DisposeAsync().AsTask();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Dynamo:ServiceUrl", _dynamo.GetConnectionString());
        builder.UseSetting("Dynamo:CreateTablesOnStartup", "true");
        builder.UseSetting("Tables:portfolio", "test-portfolio");
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
```

`tests/PDS.IntegrationTests/HostEndpointsTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PDS.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class HostEndpointsTests(ApiFactory factory)
{
    [Fact]
    public async Task Config_is_anonymous_and_returns_deployment_defaults()
    {
        var config = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/config");

        Assert.Equal("Property Development System", config.GetProperty("productName").GetString());
        Assert.Equal("teal", config.GetProperty("primaryColor").GetString());
        Assert.Equal("NZD", config.GetProperty("currency").GetString());
        Assert.Equal("en-NZ", config.GetProperty("culture").GetString());
    }

    [Fact]
    public async Task Valid_correlation_id_is_echoed()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/config");
        request.Headers.Add("X-Correlation-Id", "abc-123");

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal("abc-123", response.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Theory]
    [InlineData("bad id!")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task Invalid_correlation_id_is_replaced(string incoming)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/config");
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", incoming);

        var response = await factory.CreateClient().SendAsync(request);

        var echoed = response.Headers.GetValues("X-Correlation-Id").Single();
        Assert.NotEqual(incoming, echoed);
        Assert.Equal(32, echoed.Length);
    }

    [Fact]
    public async Task Unknown_route_returns_problem_details()
    {
        var response = await factory.CreateClient().GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.TryGetProperty("correlationId", out _));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/PDS.IntegrationTests`
Expected: build FAILS (`Program` not found; the API project has no entry point).

- [ ] **Step 4: Implement the host**

`src/PDS.Api/Settings.cs`:
```csharp
using PDS.Shared.Settings;

namespace PDS.Api;

public sealed class BrandingOptions
{
    public const string Section = "Branding";

    public string ProductName { get; set; } = "Property Development System";

    public string? LogoUrl { get; set; }

    public string PrimaryColor { get; set; } = "teal";
}

internal static class SettingsServiceCollectionExtensions
{
    public static IServiceCollection AddPdsSettings(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<BrandingOptions>().Bind(configuration.GetSection(BrandingOptions.Section));
        services.AddOptions<LocaleOptions>().Bind(configuration.GetSection(LocaleOptions.Section));
        return services;
    }
}
```

`src/PDS.Api/HostEndpoints.cs`:
```csharp
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using PDS.Shared.Settings;

namespace PDS.Api;

public sealed record ClientConfig(
    string ProductName, string? LogoUrl, string PrimaryColor, string Currency, string Culture, string TimeZone);

internal static class HostEndpoints
{
    public static IEndpointRouteBuilder MapHostEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/config", GetConfig).AllowAnonymous().WithName("GetConfig").WithTags("Host");
        return app;
    }

    internal static Ok<ClientConfig> GetConfig(IOptions<BrandingOptions> branding, IOptions<LocaleOptions> locale)
    {
        var b = branding.Value;
        var l = locale.Value;
        return TypedResults.Ok(new ClientConfig(b.ProductName, b.LogoUrl, b.PrimaryColor, l.Currency, l.Culture, l.TimeZone));
    }
}
```

`src/PDS.Api/CorrelationIdMiddleware.cs`:
```csharp
using Serilog.Context;

namespace PDS.Api;

/// <summary>Accepts a safe incoming X-Correlation-Id or generates one; echoes it and adds it to every log line.</summary>
internal sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string Header = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[Header].ToString();
        var id = IsValid(incoming) ? incoming : Guid.NewGuid().ToString("N");
        context.TraceIdentifier = id;
        context.Response.Headers[Header] = id;

        using (LogContext.PushProperty("CorrelationId", id))
        {
            await next(context);
        }
    }

    internal static bool IsValid(string value) =>
        value.Length is > 0 and <= 64 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
```

`src/PDS.Api/Program.cs`:
```csharp
using System.Text.Json.Serialization;
using Amazon.Lambda.AspNetCoreServer.Hosting;
using PDS.Api;
using PDS.Shared.Data;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog(logger =>
{
    logger.MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .Enrich.FromLogContext();
    if (builder.Environment.IsDevelopment())
        logger.WriteTo.Console();
    else
        logger.WriteTo.Console(new CompactJsonFormatter());
});
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);
builder.Services.ConfigureHttpJsonOptions(o =>
{
    // Strict numbers keep the OpenAPI types plain (number, not number | string) and reject "12" for 12.
    o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
});
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["correlationId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddPdsSettings(builder.Configuration);
builder.Services.AddDynamo();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging(o => o.EnrichDiagnosticContext = (diagnostics, http) =>
    diagnostics.Set("UserName", http.User.Identity?.Name ?? "anonymous"));
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = ex => ex is BadHttpRequestException bad ? bad.StatusCode : StatusCodes.Status500InternalServerError,
});
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapHostEndpoints();

await app.RunAsync();

public partial class Program;
```

`src/PDS.Api/appsettings.json`:
```json
{
  "AllowedHosts": "*",
  "Branding": {
    "ProductName": "Property Development System",
    "PrimaryColor": "teal"
  },
  "Locale": {
    "Currency": "NZD",
    "Culture": "en-NZ",
    "TimeZone": "Pacific/Auckland"
  }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test`
Expected: PASS for all `PDS.Tests` and `PDS.IntegrationTests` tests. The first run pulls `amazon/dynamodb-local:3.3.1`.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(api): host with config endpoint, correlation ids and problem details

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Authentication, roles and the current user

**Files:**
- Create: `src/PDS.Api/Security/AuthOptions.cs`, `Security/SecurityServiceCollectionExtensions.cs`, `Security/GatewayAuthenticationHandler.cs`, `Security/DevelopmentAuthenticationHandler.cs`, `Security/CognitoGroupsClaimsTransformation.cs`, `Security/HttpCurrentUser.cs`
- Modify: `src/PDS.Api/Program.cs` (register security, add middleware), `src/PDS.Api/HostEndpoints.cs` (replace whole file)
- Test: `tests/PDS.Tests/Api/CognitoGroupsClaimsTransformationTests.cs`, `tests/PDS.Tests/Api/AuthenticationHandlerTests.cs`, `tests/PDS.IntegrationTests/TestAuthHandler.cs`, `tests/PDS.IntegrationTests/SecurityTests.cs`
- Modify: `tests/PDS.IntegrationTests/ApiFactory.cs`

**Interfaces:**
- Consumes: `Roles`, `Policies`, `ICurrentUser` (Task 1); `HostEndpoints` (Task 2).
- Produces: `enum AuthMode { Gateway, Development }`; `AuthOptions { Mode, Authority, ClientId, LogoutDomain, DevUser }` (section `Auth`); `AddPdsSecurity(IServiceCollection, IConfiguration)`; `ICurrentUser` implementation (scoped); `GET /api/me` → `Me(string Id, string Name, string? Email, string[] Roles)`; `ClientConfig` gains `ClientAuthConfig Auth` (`Mode`, `Authority`, `ClientId`, `LogoutDomain`); test helper `ApiFactory.CreateClientAs(string roles)` (comma-separated role list; `""` = no groups).

- [ ] **Step 1: Write the failing unit tests**

`tests/PDS.Tests/Api/CognitoGroupsClaimsTransformationTests.cs`:
```csharp
using System.Security.Claims;
using PDS.Api.Security;

namespace PDS.Tests.Api;

public class CognitoGroupsClaimsTransformationTests
{
    private static ClaimsPrincipal Authenticated(params string[] groupClaimValues) =>
        new(new ClaimsIdentity(
            groupClaimValues.Select(v => new Claim("cognito:groups", v)).Append(new Claim("sub", "u1")),
            "Test", "name", ClaimTypes.Role));

    private static Task<ClaimsPrincipal> Transform(ClaimsPrincipal principal) =>
        new CognitoGroupsClaimsTransformation().TransformAsync(principal);

    [Fact]
    public async Task Maps_separate_group_claims_to_roles()
    {
        var user = await Transform(Authenticated("Admin", "Viewer"));

        Assert.True(user.IsInRole("Admin"));
        Assert.True(user.IsInRole("Viewer"));
        Assert.False(user.IsInRole("Manager"));
    }

    [Fact]
    public async Task Maps_api_gateway_bracketed_group_string_to_roles()
    {
        var user = await Transform(Authenticated("[Manager Viewer]"));

        Assert.True(user.IsInRole("Manager"));
        Assert.True(user.IsInRole("Viewer"));
    }

    [Fact]
    public async Task Maps_group_names_case_insensitively_to_canonical_roles()
    {
        var user = await Transform(Authenticated("[manager]"));

        Assert.True(user.IsInRole("Manager"));
    }

    [Fact]
    public async Task Ignores_unknown_groups()
    {
        var user = await Transform(Authenticated("[Accounting]"));

        Assert.DoesNotContain(user.Claims, c => c.Type == ClaimTypes.Role);
    }

    [Fact]
    public async Task Is_idempotent()
    {
        var once = await Transform(Authenticated("[Admin]"));
        var twice = await Transform(once);

        Assert.Single(twice.Claims, c => c.Type == ClaimTypes.Role);
    }

    [Fact]
    public async Task Leaves_anonymous_users_unchanged()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await Transform(anonymous);

        Assert.Same(anonymous, result);
    }
}
```

`tests/PDS.Tests/Api/AuthenticationHandlerTests.cs`:
```csharp
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.AspNetCoreServer;
using Microsoft.AspNetCore.Http;
using PDS.Api.Security;

namespace PDS.Tests.Api;

public class AuthenticationHandlerTests
{
    [Fact]
    public void Gateway_builds_principal_from_authorizer_jwt_claims()
    {
        var context = new DefaultHttpContext();
        context.Items[AbstractAspNetCoreFunction.LAMBDA_REQUEST_OBJECT] = new APIGatewayHttpApiV2ProxyRequest
        {
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayHttpApiV2ProxyRequest.AuthorizerDescription
                {
                    Jwt = new APIGatewayHttpApiV2ProxyRequest.AuthorizerDescription.JwtDescription
                    {
                        Claims = new Dictionary<string, string>
                        {
                            ["sub"] = "abc",
                            ["email"] = "sam@example.com",
                            ["cognito:groups"] = "[Manager]",
                        },
                    },
                },
            },
        };

        var principal = GatewayAuthenticationHandler.PrincipalFromAuthorizer(context, "Gateway");

        Assert.NotNull(principal);
        Assert.True(principal.Identity!.IsAuthenticated);
        Assert.Equal("abc", principal.FindFirst("sub")?.Value);
        Assert.Equal("[Manager]", principal.FindFirst("cognito:groups")?.Value);
    }

    [Fact]
    public void Gateway_returns_null_without_authorizer_claims() =>
        Assert.Null(GatewayAuthenticationHandler.PrincipalFromAuthorizer(new DefaultHttpContext(), "Gateway"));

    [Fact]
    public void Development_user_carries_configured_roles_as_groups()
    {
        var principal = DevelopmentAuthenticationHandler.BuildPrincipal(
            new DevUserOptions { Id = "dev", Name = "Dev User", Email = "dev@example.com", Roles = ["Viewer"] },
            "Development");

        Assert.Equal("dev", principal.FindFirst("sub")?.Value);
        Assert.Equal("Dev User", principal.Identity?.Name);
        Assert.Equal(new[] { "Viewer" }, principal.FindAll("cognito:groups").Select(c => c.Value));
    }
}
```

- [ ] **Step 2: Write the failing integration tests**

`tests/PDS.IntegrationTests/TestAuthHandler.cs`:
```csharp
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PDS.IntegrationTests;

/// <summary>
/// Signs in a test user when the X-Test-Role header is present. The roles are sent in API Gateway's bracketed
/// cognito:groups format, so the real claims transformation is exercised.
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string RoleHeader = "X-Test-Role";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RoleHeader, out var roles))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new[]
        {
            new Claim("sub", "test-user"),
            new Claim("name", "Test User"),
            new Claim("email", "test@example.com"),
            new Claim("cognito:groups", $"[{roles.ToString().Replace(',', ' ')}]"),
        };
        var identity = new ClaimsIdentity(claims, SchemeName, "name", ClaimTypes.Role);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
```

Replace `tests/PDS.IntegrationTests/ApiFactory.cs` with:
```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.DynamoDb;

namespace PDS.IntegrationTests;

/// <summary>One API host plus one DynamoDB Local container, shared by every test in the "api" collection.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly DynamoDbContainer _dynamo = new DynamoDbBuilder("amazon/dynamodb-local:3.3.1").Build();

    public async Task InitializeAsync() => await _dynamo.StartAsync();

    Task IAsyncLifetime.DisposeAsync() => _dynamo.DisposeAsync().AsTask();

    /// <summary>Client signed in with the given comma-separated roles ("" = signed in with no groups).</summary>
    public HttpClient CreateClientAs(string roles)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, roles);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Dynamo:ServiceUrl", _dynamo.GetConnectionString());
        builder.UseSetting("Dynamo:CreateTablesOnStartup", "true");
        builder.UseSetting("Tables:portfolio", "test-portfolio");
        builder.ConfigureTestServices(services => services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { }));
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
```

`tests/PDS.IntegrationTests/SecurityTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PDS.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class SecurityTests(ApiFactory factory)
{
    [Fact]
    public async Task Me_requires_authentication()
    {
        var response = await factory.CreateClient().GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Me_returns_identity_and_roles()
    {
        var me = await factory.CreateClientAs("Manager").GetFromJsonAsync<JsonElement>("/api/me");

        Assert.Equal("test-user", me.GetProperty("id").GetString());
        Assert.Equal("Test User", me.GetProperty("name").GetString());
        Assert.Equal(new string?[] { "Manager" }, me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
    }

    [Fact]
    public async Task Signed_in_user_without_groups_is_forbidden()
    {
        var response = await factory.CreateClientAs("").GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Config_exposes_gateway_auth_mode()
    {
        var config = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/config");

        Assert.Equal("Gateway", config.GetProperty("auth").GetProperty("mode").GetString());
    }

    [Fact]
    public void Development_auth_mode_is_refused_outside_development()
    {
        using var production = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.UseSetting("Auth:Mode", "Development");
        });

        var error = Assert.ThrowsAny<Exception>(() => production.CreateClient());
        Assert.Contains("Development", error.ToString());
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test`
Expected: build FAILS (`PDS.Api.Security` namespace not found).

- [ ] **Step 4: Implement security**

`src/PDS.Api/Security/AuthOptions.cs`:
```csharp
namespace PDS.Api.Security;

public enum AuthMode
{
    Gateway,
    Development,
}

public sealed class AuthOptions
{
    public const string Section = "Auth";

    public AuthMode Mode { get; set; } = AuthMode.Gateway;

    /// <summary>Cognito issuer, e.g. https://cognito-idp.ap-southeast-2.amazonaws.com/ap-southeast-2_abc.</summary>
    public string? Authority { get; set; }

    public string? ClientId { get; set; }

    /// <summary>Cognito hosted UI base URL, used by the SPA to sign out.</summary>
    public string? LogoutDomain { get; set; }

    public DevUserOptions DevUser { get; set; } = new();
}

public sealed class DevUserOptions
{
    public string Id { get; set; } = "dev-user";

    public string Name { get; set; } = "Dev User";

    public string? Email { get; set; } = "dev@example.com";

    public string[] Roles { get; set; } = [];
}

internal static class SchemeNames
{
    public const string Selector = "Pds";
    public const string Gateway = "Gateway";
    public const string Development = "Development";
}
```

`src/PDS.Api/Security/GatewayAuthenticationHandler.cs`:
```csharp
using System.Security.Claims;
using System.Text.Encodings.Web;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.AspNetCoreServer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace PDS.Api.Security;

/// <summary>
/// Trusts the claims that API Gateway's JWT authorizer has already validated. The Lambda adapter stores the
/// original request in HttpContext.Items; routes without an authorizer (GET /api/config) stay anonymous.
/// </summary>
internal sealed class GatewayAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var principal = PrincipalFromAuthorizer(Context, Scheme.Name);
        return Task.FromResult(principal is null
            ? AuthenticateResult.NoResult()
            : AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }

    internal static ClaimsPrincipal? PrincipalFromAuthorizer(HttpContext context, string scheme)
    {
        if (!context.Items.TryGetValue(AbstractAspNetCoreFunction.LAMBDA_REQUEST_OBJECT, out var raw)
            || raw is not APIGatewayHttpApiV2ProxyRequest request
            || request.RequestContext?.Authorizer?.Jwt?.Claims is not { Count: > 0 } claims)
        {
            return null;
        }

        var identity = new ClaimsIdentity(
            claims.Select(c => new Claim(c.Key, c.Value)), scheme, "name", ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }
}
```

`src/PDS.Api/Security/DevelopmentAuthenticationHandler.cs`:
```csharp
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace PDS.Api.Security;

/// <summary>Signs every request in as the configured dev user. Refused at startup outside Development.</summary>
internal sealed class DevelopmentAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<AuthOptions> auth)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(BuildPrincipal(auth.Value.DevUser, Scheme.Name), Scheme.Name)));

    internal static ClaimsPrincipal BuildPrincipal(DevUserOptions user, string scheme)
    {
        var claims = new List<Claim> { new("sub", user.Id), new("name", user.Name) };
        if (user.Email is { Length: > 0 } email)
            claims.Add(new Claim("email", email));
        claims.AddRange(user.Roles.Select(r => new Claim(CognitoGroupsClaimsTransformation.GroupsClaim, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, scheme, "name", ClaimTypes.Role));
    }
}
```

`src/PDS.Api/Security/CognitoGroupsClaimsTransformation.cs`:
```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using PDS.Shared.Security;

namespace PDS.Api.Security;

/// <summary>
/// Maps cognito:groups to role claims. Groups arrive either as separate claims or, via API Gateway, as one
/// bracketed string such as "[Admin Manager]". Matching is case-insensitive; unknown groups are ignored.
/// </summary>
internal sealed class CognitoGroupsClaimsTransformation : IClaimsTransformation
{
    public const string GroupsClaim = "cognito:groups";
    private const string RolesIdentityType = "pds-roles";

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true
            || principal.Identities.Any(i => i.AuthenticationType == RolesIdentityType))
        {
            return Task.FromResult(principal);
        }

        var roles = ParseGroups(principal.FindAll(GroupsClaim).Select(c => c.Value))
            .Select(g => Roles.All.FirstOrDefault(r => string.Equals(r, g, StringComparison.OrdinalIgnoreCase)))
            .OfType<string>()
            .Distinct()
            .ToList();
        if (roles.Count == 0)
            return Task.FromResult(principal);

        var transformed = principal.Clone();
        transformed.AddIdentity(new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), RolesIdentityType));
        return Task.FromResult(transformed);
    }

    internal static IEnumerable<string> ParseGroups(IEnumerable<string> values) =>
        values.SelectMany(v => v.Trim().TrimStart('[').TrimEnd(']')
            .Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
```

`src/PDS.Api/Security/HttpCurrentUser.cs`:
```csharp
using System.Security.Claims;
using PDS.Shared.Security;

namespace PDS.Api.Security;

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal User => accessor.HttpContext?.User ?? new ClaimsPrincipal();

    public string Id => User.FindFirstValue("sub") ?? "anonymous";

    public string Name =>
        User.FindFirstValue("name") ?? User.FindFirstValue("email") ?? User.FindFirstValue("cognito:username") ?? Id;

    public string? Email => User.FindFirstValue("email");

    public IReadOnlyList<string> Roles => PDS.Shared.Security.Roles.All.Where(User.IsInRole).ToArray();
}
```

`src/PDS.Api/Security/SecurityServiceCollectionExtensions.cs`:
```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PDS.Shared.Security;

namespace PDS.Api.Security;

internal static class SecurityServiceCollectionExtensions
{
    public static IServiceCollection AddPdsSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.Section))
            .Validate<IHostEnvironment>(
                (options, env) => options.Mode != AuthMode.Development || env.IsDevelopment(),
                "Auth:Mode=Development is only allowed when ASPNETCORE_ENVIRONMENT is Development.")
            .ValidateOnStart();

        services.AddAuthentication(SchemeNames.Selector)
            .AddPolicyScheme(SchemeNames.Selector, null, o => o.ForwardDefaultSelector = context =>
                context.RequestServices.GetRequiredService<IOptions<AuthOptions>>().Value.Mode == AuthMode.Development
                    ? SchemeNames.Development
                    : SchemeNames.Gateway)
            .AddScheme<AuthenticationSchemeOptions, GatewayAuthenticationHandler>(SchemeNames.Gateway, null)
            .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(SchemeNames.Development, null);

        services.AddTransient<IClaimsTransformation, CognitoGroupsClaimsTransformation>();
        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.CanRead, p => p.RequireRole(Roles.Viewer, Roles.Manager, Roles.Admin))
            .AddPolicy(Policies.CanWrite, p => p.RequireRole(Roles.Manager, Roles.Admin))
            .AddPolicy(Policies.CanAdminister, p => p.RequireRole(Roles.Admin));
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        return services;
    }
}
```

Replace `src/PDS.Api/HostEndpoints.cs` with:
```csharp
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using PDS.Api.Security;
using PDS.Shared.Security;
using PDS.Shared.Settings;

namespace PDS.Api;

public sealed record ClientAuthConfig(AuthMode Mode, string? Authority, string? ClientId, string? LogoutDomain);

public sealed record ClientConfig(
    string ProductName,
    string? LogoUrl,
    string PrimaryColor,
    string Currency,
    string Culture,
    string TimeZone,
    ClientAuthConfig Auth);

public sealed record Me(string Id, string Name, string? Email, string[] Roles);

internal static class HostEndpoints
{
    public static IEndpointRouteBuilder MapHostEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/config", GetConfig).AllowAnonymous().WithName("GetConfig").WithTags("Host");
        app.MapGet("/api/me", GetMe).RequireAuthorization(Policies.CanRead).WithName("GetMe").WithTags("Host");
        return app;
    }

    internal static Ok<ClientConfig> GetConfig(
        IOptions<BrandingOptions> branding, IOptions<LocaleOptions> locale, IOptions<AuthOptions> auth)
    {
        var b = branding.Value;
        var l = locale.Value;
        var a = auth.Value;
        return TypedResults.Ok(new ClientConfig(
            b.ProductName, b.LogoUrl, b.PrimaryColor, l.Currency, l.Culture, l.TimeZone,
            new ClientAuthConfig(a.Mode, a.Authority, a.ClientId, a.LogoutDomain)));
    }

    internal static Ok<Me> GetMe(ICurrentUser user) =>
        TypedResults.Ok(new Me(user.Id, user.Name, user.Email, [.. user.Roles]));
}
```

In `src/PDS.Api/Program.cs`, add `using PDS.Api.Security;` to the usings, add this line after `builder.Services.AddPdsSettings(builder.Configuration);`:
```csharp
builder.Services.AddPdsSecurity(builder.Configuration);
```
and add these two lines after `app.UseStatusCodePages();`:
```csharp
app.UseAuthentication();
app.UseAuthorization();
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test`
Expected: PASS, including `CognitoGroupsClaimsTransformationTests` (Review Focus 1) and `SecurityTests`.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(api): gateway and development authentication, role policies, /api/me

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Portfolio domain model and DynamoDB item mapping

**Files:**
- Create: `src/Modules/PDS.Portfolio/PDS.Portfolio.csproj`
- Create: `src/Modules/PDS.Portfolio/Contracts/ProjectId.cs`
- Create: `src/Modules/PDS.Portfolio/Domain/ProjectStage.cs`, `Domain/ProjectStatus.cs`, `Domain/Site.cs`, `Domain/ProjectFields.cs`, `Domain/AuditStamp.cs`, `Domain/ProjectRules.cs`, `Domain/Project.cs`
- Create: `src/Modules/PDS.Portfolio/Data/PortfolioTable.cs`, `Data/ProjectItemMapper.cs`
- Test: `tests/PDS.Tests/Portfolio/ProjectTests.cs`, `tests/PDS.Tests/Portfolio/ProjectItemMapperTests.cs`, `tests/PDS.Tests/Portfolio/ProjectSamples.cs`

**Interfaces:**
- Consumes: `Money`, `Attr`, `TableDefinition`, `GlobalIndex` (Task 1).
- Produces (public): `readonly record struct ProjectId(Guid Value)` with `static ProjectId New()`.
- Produces (internal): `enum ProjectStage { Acquisition, Feasibility, Design, Consenting, Construction, Sales, Completed }`; `enum ProjectStatus { OnTrack, AtRisk, Delayed, OnHold, Cancelled }`; `record Site(string AddressLine, string? Suburb, string City, string? Region, string? Postcode, string? LegalDescription, string? TitleReference, decimal? LandAreaSqm)`; `record ProjectFields(string Code, string Name, Site Site, ProjectStage Stage, ProjectStatus Status, DateOnly? PlannedStart, DateOnly? PlannedCompletion, DateOnly? ActualStart, DateOnly? ActualCompletion, Money? Budget, string? ProjectManager, string? Description)`; `readonly record struct AuditStamp(DateTimeOffset At, string By)`; `ProjectRules` (length constants, `NormaliseCode`, `IsValidCode`, `IsOrdered`, `Normalise`, `EnsureValid`); `class Project` (`Id`, `Fields`, `IsArchived`, `Created`, `Updated`, `Version`, `static Create(ProjectFields, AuditStamp)`, `static Rehydrate(...)`, `Update(ProjectFields, AuditStamp)`, `bool SetArchived(bool, AuditStamp)`); `PortfolioTable.LogicalName = "portfolio"`, `PortfolioTable.Gsi1 = "gsi1"`, `PortfolioTable.Definition`; `ProjectItemMapper` (`ProjectType`, `Key(ProjectId)`, `CodeGuardKey(string)`, `CodeGuard(Project)`, `ToItem(Project)`, `FromItem(IReadOnlyDictionary<string, AttributeValue>)`).

- [ ] **Step 1: Create the module project**

`src/Modules/PDS.Portfolio/PDS.Portfolio.csproj`:
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
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="PDS.Tests" />
  </ItemGroup>
</Project>
```

Add `<ProjectReference Include="../../src/Modules/PDS.Portfolio/PDS.Portfolio.csproj" />` to `tests/PDS.Tests/PDS.Tests.csproj`.

Run: `dotnet sln PDS.slnx add src/Modules/PDS.Portfolio/PDS.Portfolio.csproj`

- [ ] **Step 2: Write the failing tests**

`tests/PDS.Tests/Portfolio/ProjectSamples.cs`:
```csharp
using PDS.Portfolio.Domain;
using PDS.Shared;

namespace PDS.Tests.Portfolio;

internal static class ProjectSamples
{
    public static readonly AuditStamp Stamp = new(new DateTimeOffset(2026, 9, 29, 0, 0, 0, TimeSpan.Zero), "Sam");

    public static ProjectFields Fields(string code = "PDS-001") => new(
        Code: code,
        Name: "Harbour View Terraces",
        Site: new Site("12 Quay Street", "Auckland Central", "Auckland", "Auckland", "1010",
            "Lot 1 DP 12345", "NA123/45", 2450.5m),
        Stage: ProjectStage.Feasibility,
        Status: ProjectStatus.OnTrack,
        PlannedStart: new DateOnly(2026, 10, 1),
        PlannedCompletion: new DateOnly(2028, 3, 31),
        ActualStart: null,
        ActualCompletion: null,
        Budget: new Money(12_500_000.50m, "NZD"),
        ProjectManager: "Alex Chen",
        Description: "Twelve terraced homes.");

    public static ProjectFields Minimal(string code = "PDS-002") => new(
        code, "Minimal", new Site("1 Main Road", null, "Hamilton", null, null, null, null, null),
        ProjectStage.Acquisition, ProjectStatus.OnHold, null, null, null, null, null, null, null);
}
```

`tests/PDS.Tests/Portfolio/ProjectTests.cs`:
```csharp
using PDS.Portfolio.Domain;
using PDS.Shared;

namespace PDS.Tests.Portfolio;

public class ProjectTests
{
    [Fact]
    public void Create_normalises_code_and_trims_text()
    {
        var fields = ProjectSamples.Fields(" pds-001 ") with { Name = "  Harbour View  ", ProjectManager = "   " };

        var project = Project.Create(fields, ProjectSamples.Stamp);

        Assert.Equal("PDS-001", project.Fields.Code);
        Assert.Equal("Harbour View", project.Fields.Name);
        Assert.Null(project.Fields.ProjectManager);
        Assert.Equal(1, project.Version);
        Assert.False(project.IsArchived);
        Assert.Equal(ProjectSamples.Stamp, project.Created);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-PDS")]
    [InlineData("PDS 001")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTU")]
    public void Create_rejects_invalid_codes(string code) =>
        Assert.Throws<ArgumentException>(() => Project.Create(ProjectSamples.Fields(code), ProjectSamples.Stamp));

    [Fact]
    public void Create_rejects_completion_before_start()
    {
        var fields = ProjectSamples.Fields() with
        {
            PlannedStart = new DateOnly(2027, 1, 1),
            PlannedCompletion = new DateOnly(2026, 12, 31),
        };

        Assert.Throws<ArgumentException>(() => Project.Create(fields, ProjectSamples.Stamp));
    }

    [Fact]
    public void Create_rejects_negative_budget() =>
        Assert.Throws<ArgumentException>(() => Project.Create(
            ProjectSamples.Fields() with { Budget = new Money(-1m, "NZD") }, ProjectSamples.Stamp));

    [Fact]
    public void Update_replaces_fields_and_increments_version()
    {
        var project = Project.Create(ProjectSamples.Fields(), ProjectSamples.Stamp);
        var later = new AuditStamp(ProjectSamples.Stamp.At.AddDays(1), "Jo");

        project.Update(ProjectSamples.Fields() with { Name = "Renamed" }, later);

        Assert.Equal("Renamed", project.Fields.Name);
        Assert.Equal(2, project.Version);
        Assert.Equal(later, project.Updated);
        Assert.Equal(ProjectSamples.Stamp, project.Created);
    }

    [Fact]
    public void SetArchived_changes_state_once()
    {
        var project = Project.Create(ProjectSamples.Fields(), ProjectSamples.Stamp);

        Assert.True(project.SetArchived(true, ProjectSamples.Stamp));
        Assert.False(project.SetArchived(true, ProjectSamples.Stamp));
        Assert.True(project.IsArchived);
        Assert.Equal(2, project.Version);
    }
}
```

`tests/PDS.Tests/Portfolio/ProjectItemMapperTests.cs`:
```csharp
using Amazon.DynamoDBv2.Model;
using PDS.Portfolio.Data;
using PDS.Portfolio.Domain;
using PDS.Shared.Data;

namespace PDS.Tests.Portfolio;

public class ProjectItemMapperTests
{
    [Fact]
    public void Full_project_round_trips()
    {
        var project = Project.Create(ProjectSamples.Fields(), ProjectSamples.Stamp);

        var copy = ProjectItemMapper.FromItem(ProjectItemMapper.ToItem(project));

        Assert.Equal(project.Id, copy.Id);
        Assert.Equal(project.Fields, copy.Fields);
        Assert.Equal(project.Created, copy.Created);
        Assert.Equal(project.Updated, copy.Updated);
        Assert.Equal(project.Version, copy.Version);
        Assert.Equal(project.IsArchived, copy.IsArchived);
    }

    [Fact]
    public void Minimal_project_round_trips_with_nulls()
    {
        var project = Project.Create(ProjectSamples.Minimal(), ProjectSamples.Stamp);

        var copy = ProjectItemMapper.FromItem(ProjectItemMapper.ToItem(project));

        Assert.Equal(project.Fields, copy.Fields);
        Assert.Null(copy.Fields.Budget);
    }

    [Fact]
    public void Item_carries_keys_for_table_and_list_index()
    {
        var project = Project.Create(ProjectSamples.Fields(), ProjectSamples.Stamp);

        var item = ProjectItemMapper.ToItem(project);

        Assert.Equal($"PROJECT#{project.Id.Value}", item["pk"].S);
        Assert.Equal("PROJECT", item["sk"].S);
        Assert.Equal("PROJECT", item["gsi1pk"].S);
        Assert.Equal("PDS-001", item["gsi1sk"].S);
        Assert.Equal("2026-10-01", item["plannedStart"].S);
    }

    [Fact]
    public void Older_items_without_optional_attributes_still_map()
    {
        var project = Project.Create(ProjectSamples.Fields(), ProjectSamples.Stamp);
        var item = ProjectItemMapper.ToItem(project);
        item.Remove("isArchived");
        item.Remove("description");
        item["site"].M.Remove("landAreaSqm");

        var copy = ProjectItemMapper.FromItem(item);

        Assert.False(copy.IsArchived);
        Assert.Null(copy.Fields.Description);
        Assert.Null(copy.Fields.Site.LandAreaSqm);
    }

    [Fact]
    public void Code_guard_points_at_project()
    {
        var project = Project.Create(ProjectSamples.Fields(), ProjectSamples.Stamp);

        var guard = ProjectItemMapper.CodeGuard(project);

        Assert.Equal("CODE#PDS-001", guard["pk"].S);
        Assert.Equal("CODE", guard["sk"].S);
        Assert.Equal(project.Id.Value.ToString(), guard["projectId"].S);
    }

    [Fact]
    public void Table_definition_declares_the_list_index()
    {
        Assert.Equal("portfolio", PortfolioTable.Definition.LogicalName);
        Assert.Equal(new GlobalIndex("gsi1", "gsi1pk", "gsi1sk"), Assert.Single(PortfolioTable.Definition.GlobalIndexes));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/PDS.Tests`
Expected: build FAILS (`PDS.Portfolio.Domain` namespace not found).

- [ ] **Step 4: Implement the domain**

`src/Modules/PDS.Portfolio/Contracts/ProjectId.cs`:
```csharp
namespace PDS.Portfolio.Contracts;

public readonly record struct ProjectId(Guid Value)
{
    public static ProjectId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
```

`src/Modules/PDS.Portfolio/Domain/ProjectStage.cs`:
```csharp
namespace PDS.Portfolio.Domain;

internal enum ProjectStage
{
    Acquisition,
    Feasibility,
    Design,
    Consenting,
    Construction,
    Sales,
    Completed,
}
```

`src/Modules/PDS.Portfolio/Domain/ProjectStatus.cs`:
```csharp
namespace PDS.Portfolio.Domain;

internal enum ProjectStatus
{
    OnTrack,
    AtRisk,
    Delayed,
    OnHold,
    Cancelled,
}
```

`src/Modules/PDS.Portfolio/Domain/Site.cs`:
```csharp
namespace PDS.Portfolio.Domain;

internal sealed record Site(
    string AddressLine,
    string? Suburb,
    string City,
    string? Region,
    string? Postcode,
    string? LegalDescription,
    string? TitleReference,
    decimal? LandAreaSqm);
```

`src/Modules/PDS.Portfolio/Domain/ProjectFields.cs`:
```csharp
using PDS.Shared;

namespace PDS.Portfolio.Domain;

/// <summary>Everything a user can edit on a project.</summary>
internal sealed record ProjectFields(
    string Code,
    string Name,
    Site Site,
    ProjectStage Stage,
    ProjectStatus Status,
    DateOnly? PlannedStart,
    DateOnly? PlannedCompletion,
    DateOnly? ActualStart,
    DateOnly? ActualCompletion,
    Money? Budget,
    string? ProjectManager,
    string? Description);
```

`src/Modules/PDS.Portfolio/Domain/AuditStamp.cs`:
```csharp
namespace PDS.Portfolio.Domain;

internal readonly record struct AuditStamp(DateTimeOffset At, string By);
```

`src/Modules/PDS.Portfolio/Domain/ProjectRules.cs`:
```csharp
using System.Text.RegularExpressions;

namespace PDS.Portfolio.Domain;

internal static partial class ProjectRules
{
    public const int NameMaxLength = 200;
    public const int AddressMaxLength = 200;
    public const int CityMaxLength = 100;
    public const int ShortTextMaxLength = 100;
    public const int PostcodeMaxLength = 20;
    public const int LegalDescriptionMaxLength = 500;
    public const int ProjectManagerMaxLength = 200;
    public const int DescriptionMaxLength = 4000;

    public static string NormaliseCode(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    public static bool IsValidCode(string normalisedCode) => CodePattern().IsMatch(normalisedCode);

    public static bool IsOrdered(DateOnly? start, DateOnly? end) => start is null || end is null || end >= start;

    public static ProjectFields Normalise(ProjectFields fields) => fields with
    {
        Code = NormaliseCode(fields.Code),
        Name = (fields.Name ?? string.Empty).Trim(),
        Site = fields.Site with
        {
            AddressLine = (fields.Site.AddressLine ?? string.Empty).Trim(),
            City = (fields.Site.City ?? string.Empty).Trim(),
            Suburb = Clean(fields.Site.Suburb),
            Region = Clean(fields.Site.Region),
            Postcode = Clean(fields.Site.Postcode),
            LegalDescription = Clean(fields.Site.LegalDescription),
            TitleReference = Clean(fields.Site.TitleReference),
        },
        ProjectManager = Clean(fields.ProjectManager),
        Description = Clean(fields.Description),
    };

    /// <summary>Guards invariants. The API validators report the same rules as field errors before this runs.</summary>
    public static void EnsureValid(ProjectFields fields)
    {
        if (!IsValidCode(fields.Code))
            throw new ArgumentException($"Invalid project code '{fields.Code}'.", nameof(fields));
        if (fields.Name.Length == 0)
            throw new ArgumentException("Project name is required.", nameof(fields));
        if (fields.Site.AddressLine.Length == 0 || fields.Site.City.Length == 0)
            throw new ArgumentException("Site address and city are required.", nameof(fields));
        if (!IsOrdered(fields.PlannedStart, fields.PlannedCompletion) || !IsOrdered(fields.ActualStart, fields.ActualCompletion))
            throw new ArgumentException("Completion dates must be on or after start dates.", nameof(fields));
        if (fields.Budget is { Amount: < 0 })
            throw new ArgumentException("Budget cannot be negative.", nameof(fields));
        if (fields.Site.LandAreaSqm is < 0)
            throw new ArgumentException("Land area cannot be negative.", nameof(fields));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("^[A-Z0-9][A-Z0-9-]{0,19}$")]
    private static partial Regex CodePattern();
}
```

`src/Modules/PDS.Portfolio/Domain/Project.cs`:
```csharp
using PDS.Portfolio.Contracts;

namespace PDS.Portfolio.Domain;

internal sealed class Project
{
    private Project(ProjectId id, ProjectFields fields, bool isArchived, AuditStamp created, AuditStamp updated, long version)
    {
        Id = id;
        Fields = fields;
        IsArchived = isArchived;
        Created = created;
        Updated = updated;
        Version = version;
    }

    public ProjectId Id { get; }

    public ProjectFields Fields { get; private set; }

    public bool IsArchived { get; private set; }

    public AuditStamp Created { get; }

    public AuditStamp Updated { get; private set; }

    /// <summary>Starts at 1 and increases by 1 on every change; used for optimistic concurrency.</summary>
    public long Version { get; private set; }

    public static Project Create(ProjectFields fields, AuditStamp stamp)
    {
        var normalised = ProjectRules.Normalise(fields);
        ProjectRules.EnsureValid(normalised);
        return new Project(ProjectId.New(), normalised, false, stamp, stamp, 1);
    }

    public static Project Rehydrate(
        ProjectId id, ProjectFields fields, bool isArchived, AuditStamp created, AuditStamp updated, long version) =>
        new(id, fields, isArchived, created, updated, version);

    public void Update(ProjectFields fields, AuditStamp stamp)
    {
        var normalised = ProjectRules.Normalise(fields);
        ProjectRules.EnsureValid(normalised);
        Fields = normalised;
        Touch(stamp);
    }

    /// <returns>False when the project was already in the requested state (nothing changed).</returns>
    public bool SetArchived(bool archived, AuditStamp stamp)
    {
        if (IsArchived == archived)
            return false;

        IsArchived = archived;
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

`src/Modules/PDS.Portfolio/Data/PortfolioTable.cs`:
```csharp
using PDS.Shared.Data;

namespace PDS.Portfolio.Data;

internal static class PortfolioTable
{
    public const string LogicalName = "portfolio";
    public const string Gsi1 = "gsi1";

    public static readonly TableDefinition Definition = new(LogicalName, [new GlobalIndex(Gsi1, "gsi1pk", "gsi1sk")]);
}
```

`src/Modules/PDS.Portfolio/Data/ProjectItemMapper.cs`:
```csharp
using Amazon.DynamoDBv2.Model;
using PDS.Portfolio.Contracts;
using PDS.Portfolio.Domain;
using PDS.Shared;
using PDS.Shared.Data;

namespace PDS.Portfolio.Data;

/// <summary>
/// Project item: pk=PROJECT#id, sk=PROJECT, gsi1pk=PROJECT, gsi1sk=code.
/// Code guard item: pk=CODE#code, sk=CODE, projectId. Optional attributes are omitted when empty.
/// </summary>
internal static class ProjectItemMapper
{
    public const string ProjectType = "PROJECT";

    public static Dictionary<string, AttributeValue> Key(ProjectId id) => new()
    {
        [TableKeys.PartitionKey] = Attr.S($"PROJECT#{id.Value}"),
        [TableKeys.SortKey] = Attr.S(ProjectType),
    };

    public static Dictionary<string, AttributeValue> CodeGuardKey(string code) => new()
    {
        [TableKeys.PartitionKey] = Attr.S($"CODE#{code}"),
        [TableKeys.SortKey] = Attr.S("CODE"),
    };

    public static Dictionary<string, AttributeValue> CodeGuard(Project project)
    {
        var item = CodeGuardKey(project.Fields.Code);
        item["projectId"] = Attr.S(project.Id.Value.ToString());
        return item;
    }

    public static Dictionary<string, AttributeValue> ToItem(Project project)
    {
        var f = project.Fields;
        var item = Key(project.Id);
        item["gsi1pk"] = Attr.S(ProjectType);
        item["gsi1sk"] = Attr.S(f.Code);
        item["id"] = Attr.S(project.Id.Value.ToString());
        item["code"] = Attr.S(f.Code);
        item["name"] = Attr.S(f.Name);
        item["site"] = Attr.Map(SiteToMap(f.Site));
        item["stage"] = Attr.S(f.Stage.ToString());
        item["status"] = Attr.S(f.Status.ToString());
        Attr.SetIfPresent(item, "plannedStart", f.PlannedStart);
        Attr.SetIfPresent(item, "plannedCompletion", f.PlannedCompletion);
        Attr.SetIfPresent(item, "actualStart", f.ActualStart);
        Attr.SetIfPresent(item, "actualCompletion", f.ActualCompletion);
        if (f.Budget is { } budget)
        {
            item["budgetAmount"] = Attr.N(budget.Amount);
            item["currency"] = Attr.S(budget.Currency);
        }

        Attr.SetIfPresent(item, "projectManager", f.ProjectManager);
        Attr.SetIfPresent(item, "description", f.Description);
        item["isArchived"] = Attr.Bool(project.IsArchived);
        item["createdAt"] = Attr.Timestamp(project.Created.At);
        item["createdBy"] = Attr.S(project.Created.By);
        item["updatedAt"] = Attr.Timestamp(project.Updated.At);
        item["updatedBy"] = Attr.S(project.Updated.By);
        item["version"] = Attr.N(project.Version);
        return item;
    }

    public static Project FromItem(IReadOnlyDictionary<string, AttributeValue> item)
    {
        var site = Attr.GetMap(item, "site");
        var budgetAmount = Attr.GetDecimalOrNull(item, "budgetAmount");
        var fields = new ProjectFields(
            Code: Attr.GetString(item, "code"),
            Name: Attr.GetString(item, "name"),
            Site: new Site(
                Attr.GetString(site, "addressLine"),
                Attr.GetStringOrNull(site, "suburb"),
                Attr.GetString(site, "city"),
                Attr.GetStringOrNull(site, "region"),
                Attr.GetStringOrNull(site, "postcode"),
                Attr.GetStringOrNull(site, "legalDescription"),
                Attr.GetStringOrNull(site, "titleReference"),
                Attr.GetDecimalOrNull(site, "landAreaSqm")),
            Stage: Enum.Parse<ProjectStage>(Attr.GetString(item, "stage")),
            Status: Enum.Parse<ProjectStatus>(Attr.GetString(item, "status")),
            PlannedStart: Attr.GetDateOrNull(item, "plannedStart"),
            PlannedCompletion: Attr.GetDateOrNull(item, "plannedCompletion"),
            ActualStart: Attr.GetDateOrNull(item, "actualStart"),
            ActualCompletion: Attr.GetDateOrNull(item, "actualCompletion"),
            Budget: budgetAmount is { } amount ? new Money(amount, Attr.GetString(item, "currency")) : null,
            ProjectManager: Attr.GetStringOrNull(item, "projectManager"),
            Description: Attr.GetStringOrNull(item, "description"));

        return Project.Rehydrate(
            new ProjectId(Guid.Parse(Attr.GetString(item, "id"))),
            fields,
            Attr.GetBool(item, "isArchived"),
            new AuditStamp(Attr.GetTimestamp(item, "createdAt"), Attr.GetString(item, "createdBy")),
            new AuditStamp(Attr.GetTimestamp(item, "updatedAt"), Attr.GetString(item, "updatedBy")),
            Attr.GetLong(item, "version"));
    }

    private static Dictionary<string, AttributeValue> SiteToMap(Site site)
    {
        var map = new Dictionary<string, AttributeValue>
        {
            ["addressLine"] = Attr.S(site.AddressLine),
            ["city"] = Attr.S(site.City),
        };
        Attr.SetIfPresent(map, "suburb", site.Suburb);
        Attr.SetIfPresent(map, "region", site.Region);
        Attr.SetIfPresent(map, "postcode", site.Postcode);
        Attr.SetIfPresent(map, "legalDescription", site.LegalDescription);
        Attr.SetIfPresent(map, "titleReference", site.TitleReference);
        Attr.SetIfPresent(map, "landAreaSqm", site.LandAreaSqm);
        return map;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/PDS.Tests`
Expected: PASS (`ProjectTests`, `ProjectItemMapperTests` and earlier tests).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(portfolio): project domain model and DynamoDB item mapping

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Project store, module registration, create and get endpoints

**Files:**
- Create: `src/Modules/PDS.Portfolio/Data/SaveResult.cs`, `Data/ProjectStore.cs`
- Create: `src/Modules/PDS.Portfolio/Features/ProjectDtos.cs`, `Features/ProjectInputValidator.cs`, `Features/PortfolioProblems.cs`, `Features/CreateProject.cs`, `Features/GetProject.cs`
- Create: `src/Modules/PDS.Portfolio/PortfolioModule.cs`
- Modify: `src/PDS.Api/PDS.Api.csproj` (reference the module), `src/PDS.Api/Program.cs`
- Test: `tests/PDS.IntegrationTests/Payloads.cs`, `tests/PDS.IntegrationTests/CreateAndGetProjectTests.cs`

**Interfaces:**
- Consumes: `Project`, `ProjectFields`, `ProjectRules`, `ProjectItemMapper`, `PortfolioTable` (Task 4); `TableNames`, `ICurrentUser`, `LocaleOptions`, `Policies`, `WithValidation<T>`, `Problems` (Tasks 1–3).
- Produces: `enum SaveResult { Saved, VersionConflict, CodeTaken }`; `ProjectStore` (singleton): `Task<Project?> GetAsync(ProjectId, CancellationToken)`, `Task<IReadOnlyList<Project>> ListAllAsync(CancellationToken)`, `Task<SaveResult> CreateAsync(Project, CancellationToken)`, `Task<SaveResult> UpdateAsync(Project, long expectedVersion, string previousCode, CancellationToken)`; DTOs `SiteDto`, `IProjectInput`, `CreateProjectRequest`, `UpdateProjectRequest` (+ `long Version`), `ProjectDetails` (`static From(Project, string defaultCurrency)`), `ProjectInputMapping.ToFields(this IProjectInput, string currency)`; `PortfolioProblems.CodeTaken()`, `PortfolioProblems.VersionConflict()`; `PortfolioModule.AddPortfolioModule(IServiceCollection)`, `PortfolioModule.MapPortfolioEndpoints(IEndpointRouteBuilder)`, `PortfolioModule.Tables`; routes `POST /api/portfolio/projects` and `GET /api/portfolio/projects/{id}`.

- [ ] **Step 1: Write the failing integration tests**

`tests/PDS.IntegrationTests/Payloads.cs`:
```csharp
using System.Text.Json.Nodes;

namespace PDS.IntegrationTests;

internal static class Payloads
{
    public static string UniqueCode() => ("T" + Guid.NewGuid().ToString("N"))[..12].ToUpperInvariant();

    public static JsonObject Project(string code, string? name = null) => new()
    {
        ["code"] = code,
        ["name"] = name ?? $"Project {code}",
        ["site"] = new JsonObject
        {
            ["addressLine"] = "12 Quay Street",
            ["suburb"] = "Auckland Central",
            ["city"] = "Auckland",
        },
        ["stage"] = "Feasibility",
        ["status"] = "OnTrack",
        ["plannedStart"] = "2026-10-01",
        ["plannedCompletion"] = "2028-03-31",
        ["budgetAmount"] = 12500000.50m,
        ["projectManager"] = "Alex Chen",
    };
}
```

`tests/PDS.IntegrationTests/CreateAndGetProjectTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PDS.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class CreateAndGetProjectTests(ApiFactory factory)
{
    private const string Projects = "/api/portfolio/projects";

    private static async Task<JsonElement> ErrorsOf(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        return problem.GetProperty("errors");
    }

    [Fact]
    public async Task Manager_creates_project_and_reads_it_back()
    {
        var client = factory.CreateClientAs("Manager");
        var code = Payloads.UniqueCode();

        var created = await client.PostAsJsonAsync(Projects, Payloads.Project(code.ToLowerInvariant()));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.Equal(1, body.GetProperty("version").GetInt64());
        Assert.Equal("Test User", body.GetProperty("createdBy").GetString());
        Assert.Equal("NZD", body.GetProperty("currency").GetString());
        Assert.Equal(12500000.50m, body.GetProperty("budgetAmount").GetDecimal());

        var fetched = await client.GetFromJsonAsync<JsonElement>(created.Headers.Location);
        Assert.Equal(body.GetProperty("id").GetString(), fetched.GetProperty("id").GetString());
        Assert.Equal("Auckland", fetched.GetProperty("site").GetProperty("city").GetString());
        Assert.Equal("Feasibility", fetched.GetProperty("stage").GetString());
    }

    [Fact]
    public async Task Viewer_cannot_create()
    {
        var response = await factory.CreateClientAs("Viewer").PostAsJsonAsync(Projects, Payloads.Project(Payloads.UniqueCode()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_cannot_read()
    {
        var response = await factory.CreateClient().GetAsync($"{Projects}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Missing_required_fields_are_reported_by_field()
    {
        var payload = Payloads.Project(Payloads.UniqueCode());
        payload["name"] = "  ";
        payload["site"]!["city"] = null;

        var errors = await ErrorsOf(await factory.CreateClientAs("Manager").PostAsJsonAsync(Projects, payload));

        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("site.city", out _));
    }

    [Fact]
    public async Task Create_rejects_missing_or_numeric_stage()
    {
        var client = factory.CreateClientAs("Manager");
        var missing = Payloads.Project(Payloads.UniqueCode());
        missing.Remove("stage");
        var numeric = Payloads.Project(Payloads.UniqueCode());
        numeric["stage"] = 2;
        var unknown = Payloads.Project(Payloads.UniqueCode());
        unknown["stage"] = "Demolition";

        var missingErrors = await ErrorsOf(await client.PostAsJsonAsync(Projects, missing));
        Assert.True(missingErrors.TryGetProperty("stage", out _));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Projects, numeric)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Projects, unknown)).StatusCode);
    }

    [Fact]
    public async Task Duplicate_code_ignoring_case_and_whitespace_is_rejected()
    {
        var client = factory.CreateClientAs("Manager");
        var code = Payloads.UniqueCode();
        (await client.PostAsJsonAsync(Projects, Payloads.Project(code))).EnsureSuccessStatusCode();

        var errors = await ErrorsOf(await client.PostAsJsonAsync(Projects, Payloads.Project($"  {code.ToLowerInvariant()} ")));

        Assert.Equal("Code is already in use by another project.", errors.GetProperty("code")[0].GetString());
    }

    [Fact]
    public async Task Completion_before_start_is_rejected()
    {
        var payload = Payloads.Project(Payloads.UniqueCode());
        payload["plannedCompletion"] = "2026-09-30";

        var errors = await ErrorsOf(await factory.CreateClientAs("Manager").PostAsJsonAsync(Projects, payload));

        Assert.True(errors.TryGetProperty("plannedCompletion", out _));
    }

    [Fact]
    public async Task Budget_with_more_than_two_decimals_is_rejected()
    {
        var payload = Payloads.Project(Payloads.UniqueCode());
        payload["budgetAmount"] = 10.005m;

        var errors = await ErrorsOf(await factory.CreateClientAs("Manager").PostAsJsonAsync(Projects, payload));

        Assert.True(errors.TryGetProperty("budgetAmount", out _));
    }

    [Fact]
    public async Task Malformed_json_is_a_bad_request_not_a_server_error()
    {
        var content = new StringContent("{ \"code\": ", System.Text.Encoding.UTF8, "application/json");

        var response = await factory.CreateClientAs("Manager").PostAsync(Projects, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_project_is_not_found()
    {
        var response = await factory.CreateClientAs("Viewer").GetAsync($"{Projects}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/PDS.IntegrationTests`
Expected: FAIL. The new tests get `404 Not Found` because the portfolio routes don't exist yet.

- [ ] **Step 3: Implement the store**

`src/Modules/PDS.Portfolio/Data/SaveResult.cs`:
```csharp
namespace PDS.Portfolio.Data;

internal enum SaveResult
{
    Saved,
    VersionConflict,
    CodeTaken,
}
```

`src/Modules/PDS.Portfolio/Data/ProjectStore.cs`:
```csharp
using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using PDS.Portfolio.Contracts;
using PDS.Portfolio.Domain;
using PDS.Shared.Data;

namespace PDS.Portfolio.Data;

internal sealed class ProjectStore(IAmazonDynamoDB dynamo, TableNames names)
{
    private const string ConditionalCheckFailed = "ConditionalCheckFailed";

    private string Table => names.For(PortfolioTable.LogicalName);

    public async Task<Project?> GetAsync(ProjectId id, CancellationToken ct)
    {
        var response = await dynamo.GetItemAsync(
            new GetItemRequest { TableName = Table, Key = ProjectItemMapper.Key(id), ConsistentRead = true }, ct);
        return response.Item is { Count: > 0 } item ? ProjectItemMapper.FromItem(item) : null;
    }

    /// <summary>Reads every project through the list index. Fine up to a few thousand projects (ADR-0002).</summary>
    public async Task<IReadOnlyList<Project>> ListAllAsync(CancellationToken ct)
    {
        var projects = new List<Project>();
        Dictionary<string, AttributeValue>? start = null;
        do
        {
            var response = await dynamo.QueryAsync(new QueryRequest
            {
                TableName = Table,
                IndexName = PortfolioTable.Gsi1,
                KeyConditionExpression = "gsi1pk = :type",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":type"] = Attr.S(ProjectItemMapper.ProjectType) },
                ExclusiveStartKey = start,
            }, ct);
            projects.AddRange((response.Items ?? []).Select(ProjectItemMapper.FromItem));
            start = response.LastEvaluatedKey is { Count: > 0 } next ? next : null;
        }
        while (start is not null);

        return projects;
    }

    public Task<SaveResult> CreateAsync(Project project, CancellationToken ct) =>
        TransactAsync(
            [
                new TransactWriteItem { Put = new Put { TableName = Table, Item = ProjectItemMapper.ToItem(project), ConditionExpression = "attribute_not_exists(pk)" } },
                new TransactWriteItem { Put = new Put { TableName = Table, Item = ProjectItemMapper.CodeGuard(project), ConditionExpression = "attribute_not_exists(pk)" } },
            ],
            codeGuardIndex: 1,
            ct);

    /// <summary>Writes the project only if the stored version still equals <paramref name="expectedVersion"/>.
    /// When the code changed, the old code guard is released and the new one claimed in the same transaction.</summary>
    public async Task<SaveResult> UpdateAsync(Project project, long expectedVersion, string previousCode, CancellationToken ct)
    {
        var attributeNames = new Dictionary<string, string> { ["#version"] = "version" };
        var attributeValues = new Dictionary<string, AttributeValue>
        {
            [":expected"] = new() { N = expectedVersion.ToString(CultureInfo.InvariantCulture) },
        };
        const string versionMatches = "#version = :expected";

        if (previousCode == project.Fields.Code)
        {
            try
            {
                await dynamo.PutItemAsync(new PutItemRequest
                {
                    TableName = Table,
                    Item = ProjectItemMapper.ToItem(project),
                    ConditionExpression = versionMatches,
                    ExpressionAttributeNames = attributeNames,
                    ExpressionAttributeValues = attributeValues,
                }, ct);
                return SaveResult.Saved;
            }
            catch (ConditionalCheckFailedException)
            {
                return SaveResult.VersionConflict;
            }
        }

        return await TransactAsync(
            [
                new TransactWriteItem { Put = new Put { TableName = Table, Item = ProjectItemMapper.ToItem(project), ConditionExpression = versionMatches, ExpressionAttributeNames = attributeNames, ExpressionAttributeValues = attributeValues } },
                new TransactWriteItem { Delete = new Delete { TableName = Table, Key = ProjectItemMapper.CodeGuardKey(previousCode) } },
                new TransactWriteItem { Put = new Put { TableName = Table, Item = ProjectItemMapper.CodeGuard(project), ConditionExpression = "attribute_not_exists(pk)" } },
            ],
            codeGuardIndex: 2,
            ct);
    }

    private async Task<SaveResult> TransactAsync(List<TransactWriteItem> items, int codeGuardIndex, CancellationToken ct)
    {
        try
        {
            await dynamo.TransactWriteItemsAsync(new TransactWriteItemsRequest { TransactItems = items }, ct);
            return SaveResult.Saved;
        }
        catch (TransactionCanceledException ex)
        {
            var reasons = ex.CancellationReasons ?? [];
            if (reasons.Count > 0 && reasons[0].Code == ConditionalCheckFailed)
                return SaveResult.VersionConflict;
            if (reasons.Count > codeGuardIndex && reasons[codeGuardIndex].Code == ConditionalCheckFailed)
                return SaveResult.CodeTaken;
            throw;
        }
    }
}
```

- [ ] **Step 4: Implement DTOs, validation, endpoints and module registration**

`src/Modules/PDS.Portfolio/Features/ProjectDtos.cs`:
```csharp
using PDS.Portfolio.Domain;
using PDS.Shared;

namespace PDS.Portfolio.Features;

internal sealed record SiteDto(
    string AddressLine,
    string? Suburb,
    string City,
    string? Region,
    string? Postcode,
    string? LegalDescription,
    string? TitleReference,
    decimal? LandAreaSqm)
{
    public Site ToSite() => new(AddressLine, Suburb, City, Region, Postcode, LegalDescription, TitleReference, LandAreaSqm);

    public static SiteDto From(Site s) =>
        new(s.AddressLine, s.Suburb, s.City, s.Region, s.Postcode, s.LegalDescription, s.TitleReference, s.LandAreaSqm);
}

/// <summary>Fields shared by create and update requests. Stage and status are nullable so omission is a 400.</summary>
internal interface IProjectInput
{
    string Code { get; }

    string Name { get; }

    SiteDto Site { get; }

    ProjectStage? Stage { get; }

    ProjectStatus? Status { get; }

    DateOnly? PlannedStart { get; }

    DateOnly? PlannedCompletion { get; }

    DateOnly? ActualStart { get; }

    DateOnly? ActualCompletion { get; }

    decimal? BudgetAmount { get; }

    string? ProjectManager { get; }

    string? Description { get; }
}

internal sealed record CreateProjectRequest(
    string Code,
    string Name,
    SiteDto Site,
    ProjectStage? Stage,
    ProjectStatus? Status,
    DateOnly? PlannedStart,
    DateOnly? PlannedCompletion,
    DateOnly? ActualStart,
    DateOnly? ActualCompletion,
    decimal? BudgetAmount,
    string? ProjectManager,
    string? Description) : IProjectInput;

internal sealed record UpdateProjectRequest(
    string Code,
    string Name,
    SiteDto Site,
    ProjectStage? Stage,
    ProjectStatus? Status,
    DateOnly? PlannedStart,
    DateOnly? PlannedCompletion,
    DateOnly? ActualStart,
    DateOnly? ActualCompletion,
    decimal? BudgetAmount,
    string? ProjectManager,
    string? Description,
    long Version) : IProjectInput;

internal static class ProjectInputMapping
{
    /// <summary>Call only after validation: stage and status are known to be present.</summary>
    public static ProjectFields ToFields(this IProjectInput input, string currency) => new(
        input.Code,
        input.Name,
        input.Site.ToSite(),
        input.Stage!.Value,
        input.Status!.Value,
        input.PlannedStart,
        input.PlannedCompletion,
        input.ActualStart,
        input.ActualCompletion,
        input.BudgetAmount is { } amount ? new Money(amount, currency) : null,
        input.ProjectManager,
        input.Description);
}

internal sealed record ProjectDetails(
    Guid Id,
    string Code,
    string Name,
    SiteDto Site,
    ProjectStage Stage,
    ProjectStatus Status,
    DateOnly? PlannedStart,
    DateOnly? PlannedCompletion,
    DateOnly? ActualStart,
    DateOnly? ActualCompletion,
    decimal? BudgetAmount,
    string Currency,
    string? ProjectManager,
    string? Description,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    long Version)
{
    public static ProjectDetails From(Project project, string defaultCurrency)
    {
        var f = project.Fields;
        return new ProjectDetails(
            project.Id.Value, f.Code, f.Name, SiteDto.From(f.Site), f.Stage, f.Status,
            f.PlannedStart, f.PlannedCompletion, f.ActualStart, f.ActualCompletion,
            f.Budget?.Amount, f.Budget?.Currency ?? defaultCurrency, f.ProjectManager, f.Description,
            project.IsArchived, project.Created.At, project.Created.By, project.Updated.At, project.Updated.By,
            project.Version);
    }
}
```

`src/Modules/PDS.Portfolio/Features/ProjectInputValidator.cs`:
```csharp
using FluentValidation;
using PDS.Portfolio.Domain;

namespace PDS.Portfolio.Features;

internal abstract class ProjectInputValidator<T> : AbstractValidator<T> where T : IProjectInput
{
    protected ProjectInputValidator()
    {
        RuleFor(x => x.Code).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Code is required.")
            .Must(code => ProjectRules.IsValidCode(ProjectRules.NormaliseCode(code)))
            .WithMessage("Use up to 20 letters, digits or hyphens, starting with a letter or digit.");
        RuleFor(x => x.Name).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(ProjectRules.NameMaxLength);
        RuleFor(x => x.Stage).NotNull().WithMessage("Choose a stage.").IsInEnum();
        RuleFor(x => x.Status).NotNull().WithMessage("Choose a status.").IsInEnum();
        RuleFor(x => x.Site).NotNull().WithMessage("Site is required.").SetValidator(new SiteValidator());
        RuleFor(x => x.PlannedCompletion)
            .Must((x, end) => ProjectRules.IsOrdered(x.PlannedStart, end))
            .WithMessage("Planned completion must be on or after the planned start.");
        RuleFor(x => x.ActualCompletion)
            .Must((x, end) => ProjectRules.IsOrdered(x.ActualStart, end))
            .WithMessage("Actual completion must be on or after the actual start.");
        RuleFor(x => x.BudgetAmount)
            .GreaterThanOrEqualTo(0m).WithMessage("Budget cannot be negative.")
            .PrecisionScale(18, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places.");
        RuleFor(x => x.ProjectManager).MaximumLength(ProjectRules.ProjectManagerMaxLength);
        RuleFor(x => x.Description).MaximumLength(ProjectRules.DescriptionMaxLength);
    }
}

internal sealed class SiteValidator : AbstractValidator<SiteDto>
{
    public SiteValidator()
    {
        RuleFor(x => x.AddressLine).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Address is required.")
            .MaximumLength(ProjectRules.AddressMaxLength);
        RuleFor(x => x.City).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("City is required.")
            .MaximumLength(ProjectRules.CityMaxLength);
        RuleFor(x => x.Suburb).MaximumLength(ProjectRules.ShortTextMaxLength);
        RuleFor(x => x.Region).MaximumLength(ProjectRules.ShortTextMaxLength);
        RuleFor(x => x.Postcode).MaximumLength(ProjectRules.PostcodeMaxLength);
        RuleFor(x => x.LegalDescription).MaximumLength(ProjectRules.LegalDescriptionMaxLength);
        RuleFor(x => x.TitleReference).MaximumLength(ProjectRules.ShortTextMaxLength);
        RuleFor(x => x.LandAreaSqm).GreaterThanOrEqualTo(0m).WithMessage("Land area cannot be negative.");
    }
}

internal sealed class CreateProjectValidator : ProjectInputValidator<CreateProjectRequest>;

internal sealed class UpdateProjectValidator : ProjectInputValidator<UpdateProjectRequest>
{
    public UpdateProjectValidator() => RuleFor(x => x.Version).GreaterThan(0);
}
```

`src/Modules/PDS.Portfolio/Features/PortfolioProblems.cs`:
```csharp
using Microsoft.AspNetCore.Http.HttpResults;
using PDS.Shared.Web;

namespace PDS.Portfolio.Features;

internal static class PortfolioProblems
{
    public static ValidationProblem CodeTaken() => Problems.Field("code", "Code is already in use by another project.");

    public static ProblemHttpResult VersionConflict() => Problems.Conflict(
        "This project was changed by someone else.",
        "Reload the project to see the latest changes, then apply your edits again.");
}
```

`src/Modules/PDS.Portfolio/Features/CreateProject.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Portfolio.Data;
using PDS.Portfolio.Domain;
using PDS.Shared.Security;
using PDS.Shared.Settings;
using PDS.Shared.Web;

namespace PDS.Portfolio.Features;

internal static class CreateProject
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/projects", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<CreateProjectRequest>()
            .WithName("CreateProject");

    internal static async Task<Results<Created<ProjectDetails>, ValidationProblem>> Handle(
        CreateProjectRequest request,
        ProjectStore store,
        ICurrentUser user,
        TimeProvider clock,
        IOptions<LocaleOptions> locale,
        CancellationToken ct)
    {
        var currency = locale.Value.Currency;
        var project = Project.Create(request.ToFields(currency), new AuditStamp(clock.GetUtcNow(), user.Name));

        if (await store.CreateAsync(project, ct) == SaveResult.CodeTaken)
            return PortfolioProblems.CodeTaken();

        return TypedResults.Created($"/api/portfolio/projects/{project.Id.Value}", ProjectDetails.From(project, currency));
    }
}
```

`src/Modules/PDS.Portfolio/Features/GetProject.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Portfolio.Contracts;
using PDS.Portfolio.Data;
using PDS.Shared.Settings;

namespace PDS.Portfolio.Features;

internal static class GetProject
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/projects/{id:guid}", Handle).WithName("GetProject");

    internal static async Task<Results<Ok<ProjectDetails>, NotFound>> Handle(
        Guid id, ProjectStore store, IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var project = await store.GetAsync(new ProjectId(id), ct);
        if (project is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(ProjectDetails.From(project, locale.Value.Currency));
    }
}
```

`src/Modules/PDS.Portfolio/PortfolioModule.cs`:
```csharp
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PDS.Portfolio.Data;
using PDS.Portfolio.Features;
using PDS.Shared.Data;
using PDS.Shared.Security;

namespace PDS.Portfolio;

public static class PortfolioModule
{
    /// <summary>Tables this module owns. The host creates them locally; CDK creates them in AWS.</summary>
    public static IReadOnlyList<TableDefinition> Tables { get; } = [PortfolioTable.Definition];

    public static IServiceCollection AddPortfolioModule(this IServiceCollection services)
    {
        services.AddSingleton(PortfolioTable.Definition);
        services.AddSingleton<ProjectStore>();
        services.AddValidatorsFromAssemblyContaining<CreateProjectValidator>(includeInternalTypes: true);
        return services;
    }

    public static IEndpointRouteBuilder MapPortfolioEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/portfolio").WithTags("Portfolio").RequireAuthorization(Policies.CanRead);
        CreateProject.Map(group);
        GetProject.Map(group);
        return app;
    }
}
```

In `src/PDS.Api/PDS.Api.csproj`, add `<ProjectReference Include="../Modules/PDS.Portfolio/PDS.Portfolio.csproj" />` to the existing `ProjectReference` item group.

In `src/PDS.Api/Program.cs`, add `using PDS.Portfolio;`, add `builder.Services.AddPortfolioModule();` after `builder.Services.AddDynamo();`, and add `app.MapPortfolioEndpoints();` after `app.MapHostEndpoints();`.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test`
Expected: PASS, including `Create_rejects_missing_or_numeric_stage` (Review Focus 2) and `Duplicate_code_ignoring_case_and_whitespace_is_rejected` (Review Focus 3).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(portfolio): project store with code guard, create and get endpoints

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: List, filter, search and page projects

**Files:**
- Create: `src/Modules/PDS.Portfolio/Features/ListProjects.cs`
- Modify: `src/Modules/PDS.Portfolio/PortfolioModule.cs` (map the route)
- Test: `tests/PDS.Tests/Portfolio/ListProjectsTests.cs`, `tests/PDS.IntegrationTests/ListProjectsEndpointTests.cs`

**Interfaces:**
- Consumes: `ProjectStore.ListAllAsync` (Task 5); `PagedResult<T>` (Task 1).
- Produces: `record ListProjectsQuery(ProjectStage? Stage, ProjectStatus? Status, string? Search, bool? IncludeArchived, int? Page, int? PageSize)`; `record ProjectListItem(Guid Id, string Code, string Name, ProjectStage Stage, ProjectStatus Status, string? Suburb, string City, DateOnly? PlannedCompletion, decimal? BudgetAmount, string Currency, bool IsArchived)`; `ListProjects.Apply(IEnumerable<Project>, ListProjectsQuery, string currency) → PagedResult<ProjectListItem>`; `ListProjects.DefaultPageSize = 25`, `MaxPageSize = 100`; route `GET /api/portfolio/projects`.

- [ ] **Step 1: Write the failing tests**

`tests/PDS.Tests/Portfolio/ListProjectsTests.cs`:
```csharp
using PDS.Portfolio.Domain;
using PDS.Portfolio.Features;

namespace PDS.Tests.Portfolio;

public class ListProjectsTests
{
    private static Project Make(string code, string name, ProjectStage stage, ProjectStatus status, string city = "Auckland", string? suburb = "Ponsonby", bool archived = false)
    {
        var fields = ProjectSamples.Fields(code) with
        {
            Name = name,
            Stage = stage,
            Status = status,
            Site = ProjectSamples.Fields().Site with { City = city, Suburb = suburb },
        };
        var project = Project.Create(fields, ProjectSamples.Stamp);
        if (archived)
            project.SetArchived(true, ProjectSamples.Stamp);
        return project;
    }

    private static readonly Project[] Portfolio =
    [
        Make("B-2", "Bayview Lots", ProjectStage.Design, ProjectStatus.OnTrack, "Tauranga", "Mount Maunganui"),
        Make("A-1", "Albany Apartments", ProjectStage.Construction, ProjectStatus.Delayed),
        Make("C-3", "Cuba Street Lofts", ProjectStage.Design, ProjectStatus.AtRisk, "Wellington", "Te Aro"),
        Make("D-4", "Old Depot", ProjectStage.Completed, ProjectStatus.OnTrack, archived: true),
    ];

    private static ListProjectsQuery Query(
        ProjectStage? stage = null, ProjectStatus? status = null, string? search = null,
        bool? includeArchived = null, int? page = null, int? pageSize = null) =>
        new(stage, status, search, includeArchived, page, pageSize);

    [Fact]
    public void Excludes_archived_by_default_and_orders_by_code()
    {
        var result = ListProjects.Apply(Portfolio, Query(), "NZD");

        Assert.Equal(new[] { "A-1", "B-2", "C-3" }, result.Items.Select(i => i.Code));
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public void Includes_archived_when_asked() =>
        Assert.Equal(4, ListProjects.Apply(Portfolio, Query(includeArchived: true), "NZD").TotalCount);

    [Fact]
    public void Filters_by_stage_and_status()
    {
        Assert.Equal(new[] { "B-2", "C-3" }, ListProjects.Apply(Portfolio, Query(stage: ProjectStage.Design), "NZD").Items.Select(i => i.Code));
        Assert.Equal(new[] { "C-3" }, ListProjects.Apply(Portfolio, Query(stage: ProjectStage.Design, status: ProjectStatus.AtRisk), "NZD").Items.Select(i => i.Code));
    }

    [Theory]
    [InlineData("albany", "A-1")]
    [InlineData("  TE ARO ", "C-3")]
    [InlineData("tauranga", "B-2")]
    [InlineData("c-3", "C-3")]
    public void Searches_code_name_suburb_and_city_case_insensitively(string search, string expected) =>
        Assert.Equal(new[] { expected }, ListProjects.Apply(Portfolio, Query(search: search), "NZD").Items.Select(i => i.Code));

    [Fact]
    public void Search_treats_wildcard_characters_literally() =>
        Assert.Empty(ListProjects.Apply(Portfolio, Query(search: "%"), "NZD").Items);

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(1, 1000, 1, 100)]
    [InlineData(-5, -5, 1, 1)]
    [InlineData(null, null, 1, 25)]
    public void Paging_is_clamped_and_never_overflows(int? page, int? pageSize, int expectedPage, int expectedSize)
    {
        var result = ListProjects.Apply(Portfolio, Query(page: page, pageSize: pageSize), "NZD");

        Assert.Equal(expectedPage, result.Page);
        Assert.Equal(expectedSize, result.PageSize);
        Assert.Equal(3, result.TotalCount);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(int.MaxValue)]
    public void Page_past_the_end_is_empty_with_correct_total(int page)
    {
        var result = ListProjects.Apply(Portfolio, Query(page: page, pageSize: 2), "NZD");

        Assert.Empty(result.Items);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public void Second_page_holds_the_remainder() =>
        Assert.Equal(new[] { "C-3" }, ListProjects.Apply(Portfolio, Query(page: 2, pageSize: 2), "NZD").Items.Select(i => i.Code));
}
```

`tests/PDS.IntegrationTests/ListProjectsEndpointTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PDS.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ListProjectsEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Lists_filters_and_pages_projects()
    {
        var manager = factory.CreateClientAs("Manager");
        var token = Guid.NewGuid().ToString("N")[..8];
        foreach (var (suffix, stage) in new[] { ("1", "Design"), ("2", "Design"), ("3", "Sales") })
        {
            var payload = Payloads.Project(Payloads.UniqueCode(), $"Search {token} {suffix}");
            payload["stage"] = stage;
            (await manager.PostAsJsonAsync("/api/portfolio/projects", payload)).EnsureSuccessStatusCode();
        }

        var viewer = factory.CreateClientAs("Viewer");
        var all = await viewer.GetFromJsonAsync<JsonElement>($"/api/portfolio/projects?search={token}");
        var design = await viewer.GetFromJsonAsync<JsonElement>($"/api/portfolio/projects?search={token}&stage=Design");
        var page2 = await viewer.GetFromJsonAsync<JsonElement>($"/api/portfolio/projects?search={token}&pageSize=2&page=2");

        Assert.Equal(3, all.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, design.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, page2.GetProperty("items").GetArrayLength());
        Assert.Equal(2, page2.GetProperty("page").GetInt32());
    }

    [Fact]
    public async Task Invalid_stage_filter_is_a_bad_request()
    {
        var response = await factory.CreateClientAs("Viewer").GetAsync("/api/portfolio/projects?stage=Demolition");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test`
Expected: build FAILS (`ListProjects` and `ListProjectsQuery` not found).

- [ ] **Step 3: Implement listing**

`src/Modules/PDS.Portfolio/Features/ListProjects.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Portfolio.Data;
using PDS.Portfolio.Domain;
using PDS.Shared;
using PDS.Shared.Settings;

namespace PDS.Portfolio.Features;

internal sealed record ListProjectsQuery(
    ProjectStage? Stage, ProjectStatus? Status, string? Search, bool? IncludeArchived, int? Page, int? PageSize);

internal sealed record ProjectListItem(
    Guid Id,
    string Code,
    string Name,
    ProjectStage Stage,
    ProjectStatus Status,
    string? Suburb,
    string City,
    DateOnly? PlannedCompletion,
    decimal? BudgetAmount,
    string Currency,
    bool IsArchived);

internal static class ListProjects
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/projects", Handle).WithName("ListProjects");

    internal static async Task<Ok<PagedResult<ProjectListItem>>> Handle(
        [AsParameters] ListProjectsQuery query, ProjectStore store, IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var projects = await store.ListAllAsync(ct);
        return TypedResults.Ok(Apply(projects, query, locale.Value.Currency));
    }

    internal static PagedResult<ProjectListItem> Apply(IEnumerable<Project> projects, ListProjectsQuery query, string currency)
    {
        var page = Math.Max(1, query.Page ?? 1);
        var pageSize = Math.Clamp(query.PageSize ?? DefaultPageSize, 1, MaxPageSize);
        var term = query.Search?.Trim();

        var matches = projects
            .Where(p => query.IncludeArchived == true || !p.IsArchived)
            .Where(p => query.Stage is null || p.Fields.Stage == query.Stage)
            .Where(p => query.Status is null || p.Fields.Status == query.Status)
            .Where(p => string.IsNullOrEmpty(term) || Matches(p, term))
            .OrderBy(p => p.Fields.Code, StringComparer.Ordinal)
            .ToList();

        var skip = (long)(page - 1) * pageSize;
        IReadOnlyList<ProjectListItem> items = skip >= matches.Count
            ? []
            : matches.Skip((int)skip).Take(pageSize).Select(p => ToListItem(p, currency)).ToList();

        return new PagedResult<ProjectListItem>(items, page, pageSize, matches.Count);
    }

    private static bool Matches(Project project, string term) =>
        new[] { project.Fields.Code, project.Fields.Name, project.Fields.Site.Suburb, project.Fields.Site.City }
            .Any(value => value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true);

    private static ProjectListItem ToListItem(Project p, string currency) => new(
        p.Id.Value, p.Fields.Code, p.Fields.Name, p.Fields.Stage, p.Fields.Status, p.Fields.Site.Suburb, p.Fields.Site.City,
        p.Fields.PlannedCompletion, p.Fields.Budget?.Amount, p.Fields.Budget?.Currency ?? currency, p.IsArchived);
}
```

In `PortfolioModule.MapPortfolioEndpoints`, add `ListProjects.Map(group);` before `CreateProject.Map(group);`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test`
Expected: PASS, including `Paging_is_clamped_and_never_overflows` and `Page_past_the_end_is_empty_with_correct_total` (Review Focus 4).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(portfolio): list projects with filters, search and clamped paging

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Update a project with optimistic concurrency

**Files:**
- Create: `src/Modules/PDS.Portfolio/Features/UpdateProject.cs`
- Modify: `src/Modules/PDS.Portfolio/PortfolioModule.cs`
- Test: `tests/PDS.IntegrationTests/UpdateProjectTests.cs`, and add `ProjectApi.cs` helper

**Interfaces:**
- Consumes: `ProjectStore.GetAsync/UpdateAsync`, `UpdateProjectRequest`, `UpdateProjectValidator`, `PortfolioProblems` (Task 5).
- Produces: route `PUT /api/portfolio/projects/{id}` → `200 ProjectDetails` | `400` | `404` | `409`; test helper `ProjectApi.CreateAsync(HttpClient, string code) → JsonElement`, `ProjectApi.UpdatePayload(JsonElement details) → JsonObject`.

- [ ] **Step 1: Write the failing tests**

`tests/PDS.IntegrationTests/ProjectApi.cs`:
```csharp
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PDS.IntegrationTests;

internal static class ProjectApi
{
    public const string Projects = "/api/portfolio/projects";

    public static async Task<JsonElement> CreateAsync(HttpClient client, string code)
    {
        var response = await client.PostAsJsonAsync(Projects, Payloads.Project(code));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Builds a PUT body from a ProjectDetails response (all editable fields plus version).</summary>
    public static JsonObject UpdatePayload(JsonElement details)
    {
        var body = JsonNode.Parse(details.GetRawText())!.AsObject();
        foreach (var readOnly in new[] { "id", "currency", "isArchived", "createdAt", "createdBy", "updatedAt", "updatedBy" })
            body.Remove(readOnly);
        return body;
    }
}
```

`tests/PDS.IntegrationTests/UpdateProjectTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PDS.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class UpdateProjectTests(ApiFactory factory)
{
    private static string Url(JsonElement project) => $"{ProjectApi.Projects}/{project.GetProperty("id").GetString()}";

    [Fact]
    public async Task Update_saves_changes_and_increments_version()
    {
        var client = factory.CreateClientAs("Manager");
        var project = await ProjectApi.CreateAsync(client, Payloads.UniqueCode());
        var body = ProjectApi.UpdatePayload(project);
        body["name"] = "Renamed";
        body["status"] = "AtRisk";

        var response = await client.PutAsJsonAsync(Url(project), body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Renamed", updated.GetProperty("name").GetString());
        Assert.Equal("AtRisk", updated.GetProperty("status").GetString());
        Assert.Equal(2, updated.GetProperty("version").GetInt64());
    }

    [Fact]
    public async Task Stale_version_is_a_conflict()
    {
        var client = factory.CreateClientAs("Manager");
        var project = await ProjectApi.CreateAsync(client, Payloads.UniqueCode());
        (await client.PutAsJsonAsync(Url(project), ProjectApi.UpdatePayload(project))).EnsureSuccessStatusCode();

        var stale = await client.PutAsJsonAsync(Url(project), ProjectApi.UpdatePayload(project));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var problem = await stale.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("This project was changed by someone else.", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Concurrent_updates_with_the_same_version_let_exactly_one_win()
    {
        var client = factory.CreateClientAs("Manager");
        var project = await ProjectApi.CreateAsync(client, Payloads.UniqueCode());

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(i =>
        {
            var body = ProjectApi.UpdatePayload(project);
            body["name"] = $"Writer {i}";
            return client.PutAsJsonAsync(Url(project), body);
        }));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
    }

    [Fact]
    public async Task Changing_code_releases_the_old_code()
    {
        var client = factory.CreateClientAs("Manager");
        var oldCode = Payloads.UniqueCode();
        var project = await ProjectApi.CreateAsync(client, oldCode);
        var body = ProjectApi.UpdatePayload(project);
        body["code"] = Payloads.UniqueCode();

        (await client.PutAsJsonAsync(Url(project), body)).EnsureSuccessStatusCode();

        var reuse = await client.PostAsJsonAsync(ProjectApi.Projects, Payloads.Project(oldCode));
        Assert.Equal(HttpStatusCode.Created, reuse.StatusCode);
    }

    [Fact]
    public async Task Changing_to_a_taken_code_is_rejected_on_code()
    {
        var client = factory.CreateClientAs("Manager");
        var taken = Payloads.UniqueCode();
        await ProjectApi.CreateAsync(client, taken);
        var project = await ProjectApi.CreateAsync(client, Payloads.UniqueCode());
        var body = ProjectApi.UpdatePayload(project);
        body["code"] = taken.ToLowerInvariant();

        var response = await client.PutAsJsonAsync(Url(project), body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("code", out _));
    }

    [Fact]
    public async Task Unknown_project_is_not_found()
    {
        var client = factory.CreateClientAs("Manager");
        var project = await ProjectApi.CreateAsync(client, Payloads.UniqueCode());

        var response = await client.PutAsJsonAsync($"{ProjectApi.Projects}/{Guid.NewGuid()}", ProjectApi.UpdatePayload(project));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Viewer_cannot_update()
    {
        var project = await ProjectApi.CreateAsync(factory.CreateClientAs("Manager"), Payloads.UniqueCode());

        var response = await factory.CreateClientAs("Viewer").PutAsJsonAsync(Url(project), ProjectApi.UpdatePayload(project));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/PDS.IntegrationTests`
Expected: FAIL. PUT returns `405 Method Not Allowed` because the route doesn't exist yet.

- [ ] **Step 3: Implement update**

`src/Modules/PDS.Portfolio/Features/UpdateProject.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Portfolio.Contracts;
using PDS.Portfolio.Data;
using PDS.Portfolio.Domain;
using PDS.Shared.Security;
using PDS.Shared.Settings;
using PDS.Shared.Web;

namespace PDS.Portfolio.Features;

internal static class UpdateProject
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPut("/projects/{id:guid}", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<UpdateProjectRequest>()
            .WithName("UpdateProject");

    internal static async Task<Results<Ok<ProjectDetails>, NotFound, ValidationProblem, ProblemHttpResult>> Handle(
        Guid id,
        UpdateProjectRequest request,
        ProjectStore store,
        ICurrentUser user,
        TimeProvider clock,
        IOptions<LocaleOptions> locale,
        CancellationToken ct)
    {
        var project = await store.GetAsync(new ProjectId(id), ct);
        if (project is null)
            return TypedResults.NotFound();
        if (project.Version != request.Version)
            return PortfolioProblems.VersionConflict();

        var currency = locale.Value.Currency;
        var previousCode = project.Fields.Code;
        project.Update(request.ToFields(currency), new AuditStamp(clock.GetUtcNow(), user.Name));

        return await store.UpdateAsync(project, request.Version, previousCode, ct) switch
        {
            SaveResult.Saved => TypedResults.Ok(ProjectDetails.From(project, currency)),
            SaveResult.CodeTaken => PortfolioProblems.CodeTaken(),
            _ => PortfolioProblems.VersionConflict(),
        };
    }
}
```

In `PortfolioModule.MapPortfolioEndpoints`, add `UpdateProject.Map(group);` after `GetProject.Map(group);`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(portfolio): update projects with version check and code guard swap

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Archive and restore

**Files:**
- Create: `src/Modules/PDS.Portfolio/Features/ArchiveProject.cs`
- Modify: `src/Modules/PDS.Portfolio/PortfolioModule.cs`
- Test: `tests/PDS.IntegrationTests/ArchiveProjectTests.cs`

**Interfaces:**
- Consumes: `Project.SetArchived`, `ProjectStore.UpdateAsync` (Tasks 4–5); `ProjectApi` helper (Task 7).
- Produces: `record VersionRequest(long Version)`; routes `POST /api/portfolio/projects/{id}/archive` and `POST /api/portfolio/projects/{id}/restore` (policy `CanAdminister`), each → `200 ProjectDetails` | `404` | `409`. Setting a project to the state it's already in returns 200 with no version change.

- [ ] **Step 1: Write the failing tests**

`tests/PDS.IntegrationTests/ArchiveProjectTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PDS.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ArchiveProjectTests(ApiFactory factory)
{
    private static string Url(JsonElement project, string action) =>
        $"{ProjectApi.Projects}/{project.GetProperty("id").GetString()}/{action}";

    [Fact]
    public async Task Admin_archives_and_restores()
    {
        var admin = factory.CreateClientAs("Admin");
        var project = await ProjectApi.CreateAsync(admin, Payloads.UniqueCode());

        var archived = await (await admin.PostAsJsonAsync(Url(project, "archive"), new { version = 1 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(archived.GetProperty("isArchived").GetBoolean());
        Assert.Equal(2, archived.GetProperty("version").GetInt64());

        var restored = await (await admin.PostAsJsonAsync(Url(project, "restore"), new { version = 2 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(restored.GetProperty("isArchived").GetBoolean());
        Assert.Equal(3, restored.GetProperty("version").GetInt64());
    }

    [Fact]
    public async Task Archived_projects_are_hidden_from_the_default_list()
    {
        var admin = factory.CreateClientAs("Admin");
        var code = Payloads.UniqueCode();
        var project = await ProjectApi.CreateAsync(admin, code);
        (await admin.PostAsJsonAsync(Url(project, "archive"), new { version = 1 })).EnsureSuccessStatusCode();

        var hidden = await admin.GetFromJsonAsync<JsonElement>($"{ProjectApi.Projects}?search={code}");
        var shown = await admin.GetFromJsonAsync<JsonElement>($"{ProjectApi.Projects}?search={code}&includeArchived=true");

        Assert.Equal(0, hidden.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, shown.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Archiving_twice_is_harmless()
    {
        var admin = factory.CreateClientAs("Admin");
        var project = await ProjectApi.CreateAsync(admin, Payloads.UniqueCode());
        (await admin.PostAsJsonAsync(Url(project, "archive"), new { version = 1 })).EnsureSuccessStatusCode();

        var again = await admin.PostAsJsonAsync(Url(project, "archive"), new { version = 2 });

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(2, (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetInt64());
    }

    [Fact]
    public async Task Stale_version_is_a_conflict()
    {
        var admin = factory.CreateClientAs("Admin");
        var project = await ProjectApi.CreateAsync(admin, Payloads.UniqueCode());

        var response = await admin.PostAsJsonAsync(Url(project, "archive"), new { version = 7 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Manager_cannot_archive()
    {
        var project = await ProjectApi.CreateAsync(factory.CreateClientAs("Manager"), Payloads.UniqueCode());

        var response = await factory.CreateClientAs("Manager").PostAsJsonAsync(Url(project, "archive"), new { version = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/PDS.IntegrationTests`
Expected: FAIL (`404`/`405` for the archive routes).

- [ ] **Step 3: Implement archive and restore**

`src/Modules/PDS.Portfolio/Features/ArchiveProject.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Portfolio.Contracts;
using PDS.Portfolio.Data;
using PDS.Portfolio.Domain;
using PDS.Shared.Security;
using PDS.Shared.Settings;

namespace PDS.Portfolio.Features;

internal sealed record VersionRequest(long Version);

internal static class ArchiveProject
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/projects/{id:guid}/archive",
                (Guid id, VersionRequest request, ProjectStore store, ICurrentUser user, TimeProvider clock, IOptions<LocaleOptions> locale, CancellationToken ct) =>
                    SetArchived(id, archived: true, request, store, user, clock, locale, ct))
            .RequireAuthorization(Policies.CanAdminister)
            .WithName("ArchiveProject");
        group.MapPost("/projects/{id:guid}/restore",
                (Guid id, VersionRequest request, ProjectStore store, ICurrentUser user, TimeProvider clock, IOptions<LocaleOptions> locale, CancellationToken ct) =>
                    SetArchived(id, archived: false, request, store, user, clock, locale, ct))
            .RequireAuthorization(Policies.CanAdminister)
            .WithName("RestoreProject");
    }

    internal static async Task<Results<Ok<ProjectDetails>, NotFound, ProblemHttpResult>> SetArchived(
        Guid id,
        bool archived,
        VersionRequest request,
        ProjectStore store,
        ICurrentUser user,
        TimeProvider clock,
        IOptions<LocaleOptions> locale,
        CancellationToken ct)
    {
        var project = await store.GetAsync(new ProjectId(id), ct);
        if (project is null)
            return TypedResults.NotFound();
        if (project.Version != request.Version)
            return PortfolioProblems.VersionConflict();

        var currency = locale.Value.Currency;
        if (!project.SetArchived(archived, new AuditStamp(clock.GetUtcNow(), user.Name)))
            return TypedResults.Ok(ProjectDetails.From(project, currency));

        return await store.UpdateAsync(project, request.Version, project.Fields.Code, ct) == SaveResult.Saved
            ? TypedResults.Ok(ProjectDetails.From(project, currency))
            : PortfolioProblems.VersionConflict();
    }
}
```

In `PortfolioModule.MapPortfolioEndpoints`, add `ArchiveProject.Map(group);` after `UpdateProject.Map(group);`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(portfolio): archive and restore projects (admin only)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Portfolio contracts and module boundary tests

**Files:**
- Create: `src/Modules/PDS.Portfolio/Contracts/ProjectSummary.cs`, `Contracts/IPortfolioQueries.cs`, `Data/PortfolioQueries.cs`
- Modify: `src/Modules/PDS.Portfolio/Data/ProjectStore.cs` (add `GetManyAsync`), `src/Modules/PDS.Portfolio/PortfolioModule.cs` (register queries)
- Test: `tests/PDS.Tests/Architecture/TypeReferences.cs`, `tests/PDS.Tests/Architecture/ModuleBoundaryTests.cs`, `tests/PDS.IntegrationTests/PortfolioQueriesTests.cs`

**Interfaces:**
- Consumes: `ProjectStore`, `ProjectItemMapper` (Tasks 4–5).
- Produces (public, for future modules): `sealed record ProjectSummary(ProjectId Id, string Code, string Name, string Stage, string Status, bool IsArchived)`; `interface IPortfolioQueries { Task<bool> Exists(ProjectId, CancellationToken); Task<IReadOnlyList<ProjectSummary>> GetSummaries(IReadOnlyCollection<ProjectId>, CancellationToken); }` (scoped). Produces (internal): `ProjectStore.GetManyAsync(IReadOnlyCollection<ProjectId>, CancellationToken) → IReadOnlyList<Project>`.

- [ ] **Step 1: Write the failing tests**

`tests/PDS.Tests/Architecture/TypeReferences.cs`:
```csharp
using System.Reflection;

namespace PDS.Tests.Architecture;

/// <summary>Types a type mentions in its signatures: base type, interfaces, fields, properties, methods, constructors.
/// Method bodies are not inspected; the "only Contracts are public" rule covers what signatures miss.</summary>
internal static class TypeReferences
{
    private const BindingFlags Everything =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static IEnumerable<Type> Of(Type type)
    {
        var direct = new List<Type?> { type.BaseType };
        direct.AddRange(type.GetInterfaces());
        direct.AddRange(type.GetFields(Everything).Select(f => f.FieldType));
        direct.AddRange(type.GetProperties(Everything).Select(p => p.PropertyType));
        foreach (var method in type.GetMethods(Everything))
        {
            direct.Add(method.ReturnType);
            direct.AddRange(method.GetParameters().Select(p => p.ParameterType));
        }

        foreach (var constructor in type.GetConstructors(Everything))
            direct.AddRange(constructor.GetParameters().Select(p => p.ParameterType));

        return direct.OfType<Type>().SelectMany(Expand).Distinct();
    }

    private static IEnumerable<Type> Expand(Type type)
    {
        if (type.HasElementType)
            return Expand(type.GetElementType()!);
        if (type.IsGenericType && !type.IsGenericTypeDefinition)
            return new[] { type.GetGenericTypeDefinition() }.Concat(type.GetGenericArguments().SelectMany(Expand));
        return [type];
    }
}
```

`tests/PDS.Tests/Architecture/ModuleBoundaryTests.cs`:
```csharp
using System.Reflection;
using PDS.Portfolio;

namespace PDS.Tests.Architecture;

public class ModuleBoundaryTests
{
    /// <summary>Add each new module's assembly here.</summary>
    private static readonly Assembly[] Modules = [typeof(PortfolioModule).Assembly];

    private static bool IsContract(Type type) => type.Namespace?.EndsWith(".Contracts", StringComparison.Ordinal) == true;

    [Fact]
    public void Modules_only_expose_contracts_and_the_module_class()
    {
        var offenders = Modules
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => !IsContract(t) && !t.Name.EndsWith("Module", StringComparison.Ordinal))
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Modules_reference_other_modules_only_through_contracts()
    {
        var offenders = (
            from module in Modules
            from type in module.GetTypes()
            from used in TypeReferences.Of(type)
            where used.Assembly != module && Modules.Contains(used.Assembly) && !IsContract(used)
            select $"{type.FullName} -> {used.FullName}").ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Domain_types_do_not_depend_on_web_or_aws()
    {
        var offenders = (
            from module in Modules
            from type in module.GetTypes()
            where type.Namespace?.EndsWith(".Domain", StringComparison.Ordinal) == true
            from used in TypeReferences.Of(type)
            where used.Namespace is { } ns
                  && (ns.StartsWith("Amazon", StringComparison.Ordinal) || ns.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal))
            select $"{type.FullName} -> {used.FullName}").ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Type_references_see_fields_properties_and_signatures()
    {
        var references = TypeReferences.Of(typeof(Sample)).ToList();

        Assert.Contains(typeof(Uri), references);
        Assert.Contains(typeof(TimeSpan), references);
        Assert.Contains(typeof(Version), references);
        Assert.Contains(typeof(DateOnly), references);
    }

    private sealed class Sample
    {
        public TimeSpan Duration = TimeSpan.Zero;

        public Uri? Link { get; set; }

        public Version? Make(DateOnly day) => day.Year > 0 ? null : new Version();
    }
}
```

`tests/PDS.IntegrationTests/PortfolioQueriesTests.cs`:
```csharp
using Microsoft.Extensions.DependencyInjection;
using PDS.Portfolio.Contracts;

namespace PDS.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class PortfolioQueriesTests(ApiFactory factory)
{
    [Fact]
    public async Task Exists_and_summaries_answer_for_other_modules()
    {
        var manager = factory.CreateClientAs("Manager");
        var first = await ProjectApi.CreateAsync(manager, Payloads.UniqueCode());
        var second = await ProjectApi.CreateAsync(manager, Payloads.UniqueCode());
        var firstId = new ProjectId(Guid.Parse(first.GetProperty("id").GetString()!));
        var secondId = new ProjectId(Guid.Parse(second.GetProperty("id").GetString()!));

        using var scope = factory.Services.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<IPortfolioQueries>();

        Assert.True(await queries.Exists(firstId, CancellationToken.None));
        Assert.False(await queries.Exists(new ProjectId(Guid.NewGuid()), CancellationToken.None));

        var summaries = await queries.GetSummaries([firstId, secondId, firstId, new ProjectId(Guid.NewGuid())], CancellationToken.None);

        Assert.Equal(
            new[] { firstId, secondId }.OrderBy(i => i.Value),
            summaries.Select(s => s.Id).OrderBy(i => i.Value));
        Assert.All(summaries, s => Assert.Equal("Feasibility", s.Stage));
    }

    [Fact]
    public async Task Summaries_of_nothing_is_empty()
    {
        using var scope = factory.Services.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<IPortfolioQueries>();

        Assert.Empty(await queries.GetSummaries([], CancellationToken.None));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test`
Expected: build FAILS (`IPortfolioQueries` not found).

- [ ] **Step 3: Implement the contracts**

`src/Modules/PDS.Portfolio/Contracts/ProjectSummary.cs`:
```csharp
namespace PDS.Portfolio.Contracts;

public sealed record ProjectSummary(ProjectId Id, string Code, string Name, string Stage, string Status, bool IsArchived);
```

`src/Modules/PDS.Portfolio/Contracts/IPortfolioQueries.cs`:
```csharp
namespace PDS.Portfolio.Contracts;

/// <summary>The only way other modules may read portfolio data.</summary>
public interface IPortfolioQueries
{
    Task<bool> Exists(ProjectId id, CancellationToken ct);

    /// <summary>Summaries for the known ids; unknown ids are skipped and duplicates collapse.</summary>
    Task<IReadOnlyList<ProjectSummary>> GetSummaries(IReadOnlyCollection<ProjectId> ids, CancellationToken ct);
}
```

Add to `src/Modules/PDS.Portfolio/Data/ProjectStore.cs` (inside the class, after `ListAllAsync`):
```csharp
    /// <summary>BatchGetItem in chunks of 100 (the DynamoDB limit), retrying unprocessed keys. Duplicate ids are removed
    /// first because BatchGetItem rejects duplicate keys.</summary>
    public async Task<IReadOnlyList<Project>> GetManyAsync(IReadOnlyCollection<ProjectId> ids, CancellationToken ct)
    {
        var projects = new List<Project>();
        foreach (var chunk in ids.Distinct().Chunk(100))
        {
            var pending = new Dictionary<string, KeysAndAttributes>
            {
                [Table] = new KeysAndAttributes { Keys = chunk.Select(ProjectItemMapper.Key).ToList(), ConsistentRead = true },
            };
            while (pending.Count > 0)
            {
                var response = await dynamo.BatchGetItemAsync(new BatchGetItemRequest { RequestItems = pending }, ct);
                if (response.Responses?.TryGetValue(Table, out var items) == true)
                    projects.AddRange(items.Select(ProjectItemMapper.FromItem));
                pending = response.UnprocessedKeys is { Count: > 0 } unprocessed ? unprocessed : [];
            }
        }

        return projects;
    }
```

`src/Modules/PDS.Portfolio/Data/PortfolioQueries.cs`:
```csharp
using PDS.Portfolio.Contracts;

namespace PDS.Portfolio.Data;

internal sealed class PortfolioQueries(ProjectStore store) : IPortfolioQueries
{
    public async Task<bool> Exists(ProjectId id, CancellationToken ct) => await store.GetAsync(id, ct) is not null;

    public async Task<IReadOnlyList<ProjectSummary>> GetSummaries(IReadOnlyCollection<ProjectId> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
            return [];

        var projects = await store.GetManyAsync(ids, ct);
        return projects
            .Select(p => new ProjectSummary(p.Id, p.Fields.Code, p.Fields.Name, p.Fields.Stage.ToString(), p.Fields.Status.ToString(), p.IsArchived))
            .ToList();
    }
}
```

In `PortfolioModule.AddPortfolioModule`, add `services.AddScoped<IPortfolioQueries, PortfolioQueries>();` after the `ProjectStore` registration, and add `using PDS.Portfolio.Contracts;`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test`
Expected: PASS, including all three `ModuleBoundaryTests` rules.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(portfolio): public query contract and module boundary tests

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Local run, OpenAPI document and Lambda package

**Files:**
- Create: `docker-compose.yml`, `src/PDS.Api/appsettings.Development.json`, `src/PDS.Api/Properties/launchSettings.json`, `README.md`
- Modify: `src/PDS.Api/PDS.Api.csproj` (build-time OpenAPI generation), `Directory.Packages.props` (already lists `Microsoft.Extensions.ApiDescription.Server`)
- Generated: `web/src/shared/api/openapi.json` (committed; CI fails if it drifts)

**Interfaces:**
- Produces: API on `http://localhost:5080` in Development, signed in as the dev user; DynamoDB Local on `http://localhost:8000`; `web/src/shared/api/openapi.json` (consumed by Task 11's `npm run gen:api`); `artifacts/api/` Lambda bundle (consumed by Task 16 CDK).

- [ ] **Step 1: Add local development configuration**

`docker-compose.yml`:
```yaml
services:
  dynamodb:
    image: amazon/dynamodb-local:3.3.1
    command: ["-jar", "DynamoDBLocal.jar", "-sharedDb", "-dbPath", "/home/dynamodblocal/data"]
    ports:
      - "8000:8000"
    volumes:
      - dynamodb-data:/home/dynamodblocal/data
volumes:
  dynamodb-data:
```

`src/PDS.Api/appsettings.Development.json`:
```json
{
  "Auth": {
    "Mode": "Development",
    "DevUser": {
      "Id": "dev-user",
      "Name": "Dev User",
      "Email": "dev@example.com",
      "Roles": [ "Admin" ]
    }
  },
  "Dynamo": {
    "ServiceUrl": "http://localhost:8000",
    "CreateTablesOnStartup": true
  }
}
```

`src/PDS.Api/Properties/launchSettings.json`:
```json
{
  "profiles": {
    "PDS.Api": {
      "commandName": "Project",
      "applicationUrl": "http://localhost:5080",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

- [ ] **Step 2: Generate the OpenAPI document on Debug builds**

In `src/PDS.Api/PDS.Api.csproj`, add this property group and package reference:
```xml
  <PropertyGroup>
    <OpenApiDocumentsDirectory>$(MSBuildProjectDirectory)/../../web/src/shared/api</OpenApiDocumentsDirectory>
    <OpenApiGenerateDocumentsOptions>--file-name openapi</OpenApiGenerateDocumentsOptions>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)' != 'Debug'">
    <OpenApiGenerateDocuments>false</OpenApiGenerateDocuments>
    <OpenApiGenerateDocumentsOnBuild>false</OpenApiGenerateDocumentsOnBuild>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.ApiDescription.Server" PrivateAssets="all" IncludeAssets="runtime; build; native; contentfiles; analyzers; buildtransitive" />
  </ItemGroup>
```

Run: `mkdir -p web/src/shared/api && dotnet build`
Expected: build succeeds and `web/src/shared/api/openapi.json` exists.

Run: `grep -c '"/api/portfolio/projects' web/src/shared/api/openapi.json`
Expected: a number ≥ 3 (list/create, get/update, archive and restore paths).

- [ ] **Step 3: Run the API locally end to end**

Run: `docker compose up -d dynamodb`
Run (separate terminal): `dotnet run --project src/PDS.Api`
Expected log: `Now listening on: http://localhost:5080`.

Run:
```bash
curl -s http://localhost:5080/api/config
curl -s -X POST http://localhost:5080/api/portfolio/projects -H 'content-type: application/json' \
  -d '{"code":"demo-1","name":"Demo","site":{"addressLine":"1 Queen St","city":"Auckland"},"stage":"Design","status":"OnTrack"}'
curl -s http://localhost:5080/api/portfolio/projects
curl -s http://localhost:5080/api/me
```
Expected: config JSON with `"auth":{"mode":"Development",...}`; a `201`-style project body with `"code":"DEMO-1"`; a list containing `DEMO-1`; `/api/me` shows `"Dev User"` with roles `["Admin"]`. Stop the API with Ctrl+C.

- [ ] **Step 4: Package for Lambda**

Run: `dotnet publish src/PDS.Api -c Release -r linux-arm64 --self-contained false -p:PublishReadyToRun=true -o artifacts/api`
Expected: succeeds; `artifacts/api/PDS.Api.dll` and `artifacts/api/PDS.Api.runtimeconfig.json` exist (handler name for the managed runtime is `PDS.Api`).

- [ ] **Step 5: Write the README**

`README.md`:
````markdown
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
````

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "chore: local dev setup, build-time OpenAPI document, README

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: Web app shell — config, session, API client and layout

**Files:**
- Create: `web/package.json`, `web/tsconfig.json`, `web/vite.config.ts`, `web/eslint.config.js`, `web/.prettierrc.json`, `web/postcss.config.cjs`, `web/index.html`, `web/public/config.json`
- Create: `web/src/main.tsx`, `web/src/App.tsx`, `web/src/routes.tsx`
- Create: `web/src/shared/config.ts`, `shared/format.ts`, `shared/validation.ts`, `shared/me.ts`
- Create: `web/src/shared/api/client.ts`, `shared/api/ApiProvider.tsx`, `shared/api/types.ts`; generated `shared/api/schema.d.ts`
- Create: `web/src/shared/auth/session.tsx`
- Create: `web/src/shared/layout/AppLayout.tsx`, `shared/layout/FullPageMessage.tsx`, `shared/layout/NotFoundPage.tsx`
- Create: `web/src/features/portfolio/ProjectsPage.tsx` (placeholder, replaced in Task 12)
- Test: `web/src/test/setup.ts`, `web/src/test/server.ts`, `web/src/test/render.tsx`, `web/src/shared/format.test.ts`, `web/src/shared/config.test.ts`, `web/src/shared/layout/AppLayout.test.tsx`

**Interfaces:**
- Consumes: `web/src/shared/api/openapi.json` (Task 10), `GET /api/config`, `GET /api/me`.
- Produces: `type DeepRequired<T>`; `type AppConfig` (server config + `apiBaseUrl`), `loadConfig(fetchImpl?)`, `ConfigContext`, `useConfig()`; `type Api`, `createApi(baseUrl, { getToken, onUnauthorized? })`, `class ApiError { status; problem? }`, `type ProblemDetails`, `unwrap<T>(request) → Promise<T>`, `ApiContext`, `useApi()`, `ApiProvider`; `type Session { getToken; signIn; signOut }`, `SessionContext`, `useSession()`, `SessionProvider`; `useMe()`, `usePermissions() → { canWrite, canAdminister }`; `formatDate(iso, culture)`, `formatDateTime(iso, culture, timeZone)`, `formatMoney(amount, currency, culture)`; `zodValidate<T>(schema)`, `serverFieldErrors(problem)`; `routes: RouteObject[]`; test helpers `API`, `testConfig`, `renderApp(path, { roles?, forbidden? }) → { router, user }`, `server`.

- [ ] **Step 1: Create the web project files**

`web/package.json`:
```json
{
  "name": "pds-web",
  "private": true,
  "version": "0.1.0",
  "type": "module",
  "engines": {
    "node": ">=22.12"
  },
  "scripts": {
    "dev": "vite",
    "build": "tsc --noEmit && vite build",
    "typecheck": "tsc --noEmit",
    "lint": "eslint .",
    "format": "prettier --write .",
    "test": "vitest run",
    "gen:api": "openapi-typescript src/shared/api/openapi.json -o src/shared/api/schema.d.ts"
  },
  "dependencies": {
    "@mantine/core": "9.6.3",
    "@mantine/form": "9.6.3",
    "@mantine/hooks": "9.6.3",
    "@mantine/notifications": "9.6.3",
    "@tanstack/react-query": "5.104.0",
    "oidc-client-ts": "3.5.0",
    "openapi-fetch": "0.17.0",
    "react": "19.3.0",
    "react-dom": "19.3.0",
    "react-oidc-context": "3.3.1",
    "react-router": "8.4.0",
    "zod": "4.6.5"
  },
  "devDependencies": {
    "@eslint/js": "10.0.1",
    "@testing-library/jest-dom": "7.0.1",
    "@testing-library/react": "16.3.3",
    "@testing-library/user-event": "14.6.7",
    "@types/node": "^24.0.0",
    "@types/react": "19.3.0",
    "@types/react-dom": "19.3.0",
    "@vitejs/plugin-react": "6.1.1",
    "eslint": "10.11.0",
    "eslint-plugin-react-hooks": "7.1.1",
    "jsdom": "30.1.1",
    "msw": "3.0.0",
    "openapi-typescript": "7.13.0",
    "postcss": "8.5.28",
    "postcss-preset-mantine": "1.18.0",
    "postcss-simple-vars": "7.0.1",
    "prettier": "3.9.9",
    "typescript": "5.9.3",
    "typescript-eslint": "8.71.0",
    "vite": "8.3.1",
    "vitest": "5.0.2"
  }
}
```
TypeScript is pinned to 5.9.3 because `openapi-typescript` 7 and `typescript-eslint` 8 don't yet accept TypeScript 6+.

`web/tsconfig.json`:
```json
{
  "compilerOptions": {
    "target": "ES2022",
    "lib": ["ES2023", "DOM", "DOM.Iterable"],
    "module": "ESNext",
    "moduleResolution": "Bundler",
    "jsx": "react-jsx",
    "strict": true,
    "noEmit": true,
    "isolatedModules": true,
    "esModuleInterop": true,
    "skipLibCheck": true,
    "resolveJsonModule": true,
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "noFallthroughCasesInSwitch": true,
    "types": ["vite/client"]
  },
  "include": ["src", "vite.config.ts"]
}
```

`web/vite.config.ts`:
```ts
/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: { '/api': 'http://localhost:5080' },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: false,
    env: { TZ: 'Pacific/Auckland' },
  },
});
```

`web/eslint.config.js`:
```js
import js from '@eslint/js';
import reactHooks from 'eslint-plugin-react-hooks';
import { defineConfig } from 'eslint/config';
import tseslint from 'typescript-eslint';

export default defineConfig([
  { ignores: ['dist', 'coverage', 'src/shared/api/schema.d.ts'] },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  {
    files: ['**/*.{ts,tsx}'],
    plugins: { 'react-hooks': reactHooks },
    rules: {
      'react-hooks/rules-of-hooks': 'error',
      'react-hooks/exhaustive-deps': 'warn',
    },
  },
]);
```

`web/.prettierrc.json`:
```json
{ "singleQuote": true, "printWidth": 110 }
```

`web/postcss.config.cjs`:
```js
module.exports = {
  plugins: {
    'postcss-preset-mantine': {},
    'postcss-simple-vars': {
      variables: {
        'mantine-breakpoint-xs': '36em',
        'mantine-breakpoint-sm': '48em',
        'mantine-breakpoint-md': '62em',
        'mantine-breakpoint-lg': '75em',
        'mantine-breakpoint-xl': '88em',
      },
    },
  },
};
```

`web/index.html`:
```html
<!doctype html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Loading…</title>
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="/src/main.tsx"></script>
  </body>
</html>
```

`web/public/config.json` (local dev: same origin through the Vite proxy; CDK overwrites this file per deployment):
```json
{ "apiBaseUrl": "" }
```

Run: `cd web && npm install && npm run gen:api`
Expected: install succeeds with no peer-dependency errors; `src/shared/api/schema.d.ts` is generated and contains `"/api/portfolio/projects"`.

- [ ] **Step 2: Write the test infrastructure and failing tests**

`web/src/test/server.ts`:
```ts
import { setupServer } from 'msw/node';

export const server = setupServer();
```

`web/src/test/setup.ts`:
```ts
import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterAll, afterEach, beforeAll } from 'vitest';
import { server } from './server';

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => {
  cleanup();
  server.resetHandlers();
});
afterAll(() => server.close());

// jsdom gaps that Mantine relies on.
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  }),
});
class ResizeObserverStub {
  observe() {}
  unobserve() {}
  disconnect() {}
}
window.ResizeObserver = ResizeObserverStub as unknown as typeof ResizeObserver;
window.HTMLElement.prototype.scrollIntoView = () => {};
```

`web/src/test/render.tsx`:
```tsx
import { MantineProvider } from '@mantine/core';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { routes } from '../routes';
import { ApiContext, createApi } from '../shared/api/client';
import { SessionContext, type Session } from '../shared/auth/session';
import { ConfigContext, type AppConfig } from '../shared/config';
import { server } from './server';

export const API = 'http://localhost';

export const testConfig: AppConfig = {
  apiBaseUrl: API,
  productName: 'Test PDS',
  logoUrl: null,
  primaryColor: 'teal',
  currency: 'NZD',
  culture: 'en-NZ',
  timeZone: 'Pacific/Auckland',
  auth: { mode: 'Development', authority: null, clientId: null, logoutDomain: null },
};

export function renderApp(path: string, { roles = ['Manager'], forbidden = false }: { roles?: string[]; forbidden?: boolean } = {}) {
  server.use(
    http.get(`${API}/api/me`, () =>
      forbidden
        ? HttpResponse.json({ title: 'Forbidden', status: 403 }, { status: 403 })
        : HttpResponse.json({ id: 'u1', name: 'Sam Taylor', email: 'sam@example.com', roles }),
    ),
  );
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  const api = createApi(API, { getToken: () => undefined });
  const session: Session = { getToken: () => undefined, signIn: () => {}, signOut: () => {} };
  const router = createMemoryRouter(routes, { initialEntries: [path] });

  render(
    <ConfigContext.Provider value={testConfig}>
      <MantineProvider env="test">
        <QueryClientProvider client={queryClient}>
          <SessionContext.Provider value={session}>
            <ApiContext.Provider value={api}>
              <RouterProvider router={router} />
            </ApiContext.Provider>
          </SessionContext.Provider>
        </QueryClientProvider>
      </MantineProvider>
    </ConfigContext.Provider>,
  );
  return { router, user: userEvent.setup() };
}
```

`web/src/shared/format.test.ts`:
```ts
import { describe, expect, it } from 'vitest';
import { formatDate, formatDateTime, formatMoney } from './format';

describe('formatDate', () => {
  it('keeps a date-only value on the same calendar day in NZ time', () => {
    expect(formatDate('2026-03-01', 'en-NZ')).toBe('1 Mar 2026');
  });

  it('shows a dash when there is no date', () => {
    expect(formatDate(null, 'en-NZ')).toBe('—');
  });
});

describe('formatDateTime', () => {
  it('converts UTC timestamps to the deployment time zone', () => {
    expect(formatDateTime('2026-09-29T20:30:00Z', 'en-NZ', 'Pacific/Auckland')).toMatch(/^30 Sept? 2026/);
  });
});

describe('formatMoney', () => {
  it('shows whole amounts without cents', () => {
    expect(formatMoney(12500000, 'NZD', 'en-NZ')).toBe('$12,500,000');
  });

  it('shows cents when present', () => {
    expect(formatMoney(12500000.5, 'NZD', 'en-NZ')).toBe('$12,500,000.50');
  });

  it('shows a dash when there is no amount', () => {
    expect(formatMoney(null, 'NZD', 'en-NZ')).toBe('—');
  });
});
```

`web/src/shared/config.test.ts`:
```ts
import { describe, expect, it } from 'vitest';
import { loadConfig } from './config';

function fakeFetch(responses: Record<string, unknown>, status = 200): typeof fetch {
  return (async (input: RequestInfo | URL) => {
    const url = String(input);
    if (!(url in responses)) return new Response('missing', { status: 404 });
    return new Response(JSON.stringify(responses[url]), { status });
  }) as typeof fetch;
}

describe('loadConfig', () => {
  it('combines the bootstrap file with the server config and trims a trailing slash', async () => {
    const config = await loadConfig(
      fakeFetch({
        '/config.json': { apiBaseUrl: 'https://api.example.com/' },
        'https://api.example.com/api/config': { productName: 'PDS', currency: 'NZD' },
      }),
    );

    expect(config.apiBaseUrl).toBe('https://api.example.com');
    expect(config.productName).toBe('PDS');
  });

  it('fails with the URL when a request fails', async () => {
    await expect(loadConfig(fakeFetch({}))).rejects.toThrow('/config.json');
  });
});
```

`web/src/shared/layout/AppLayout.test.tsx`:
```tsx
import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { renderApp } from '../../test/render';

describe('AppLayout', () => {
  it('shows the configured product name and the signed-in user', async () => {
    renderApp('/projects');

    expect(await screen.findByText('Test PDS')).toBeInTheDocument();
    expect(await screen.findByText('Sam Taylor')).toBeInTheDocument();
  });

  it('explains what to do when the account has no group', async () => {
    renderApp('/projects', { forbidden: true });

    expect(await screen.findByText('No access yet')).toBeInTheDocument();
  });
});
```

Run: `cd web && npx vitest run`
Expected: FAIL (modules such as `./format`, `../routes` do not exist).

- [ ] **Step 3: Implement the shared modules**

`web/src/shared/api/types.ts`:
```ts
/** The generated schema marks every response property optional; the API always sends them. */
export type DeepRequired<T> = T extends (infer U)[]
  ? DeepRequired<U>[]
  : T extends object
    ? { [K in keyof T]-?: DeepRequired<T[K]> }
    : T;
```

`web/src/shared/config.ts`:
```ts
import { createContext, useContext } from 'react';
import type { paths } from './api/schema';
import type { DeepRequired } from './api/types';

export type ServerConfig = DeepRequired<paths['/api/config']['get']['responses'][200]['content']['application/json']>;
export type AppConfig = ServerConfig & { apiBaseUrl: string };

async function getJson<T>(fetchImpl: typeof fetch, url: string): Promise<T> {
  const response = await fetchImpl(url, { headers: { Accept: 'application/json' } });
  if (!response.ok) throw new Error(`GET ${url} failed with status ${response.status}`);
  return (await response.json()) as T;
}

/** Reads /config.json (API address for this deployment), then the server's branding, locale and auth settings. */
export async function loadConfig(fetchImpl: typeof fetch = fetch): Promise<AppConfig> {
  const bootstrap = await getJson<{ apiBaseUrl: string }>(fetchImpl, '/config.json');
  const apiBaseUrl = bootstrap.apiBaseUrl.replace(/\/+$/, '');
  const server = await getJson<ServerConfig>(fetchImpl, `${apiBaseUrl}/api/config`);
  return { ...server, apiBaseUrl };
}

export const ConfigContext = createContext<AppConfig | null>(null);

export function useConfig(): AppConfig {
  const config = useContext(ConfigContext);
  if (!config) throw new Error('useConfig must be used inside ConfigContext');
  return config;
}
```

`web/src/shared/api/client.ts`:
```ts
import createClient, { type Client, type Middleware } from 'openapi-fetch';
import { createContext, useContext } from 'react';
import type { paths } from './schema';

export type Api = Client<paths>;

export type ProblemDetails = {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
  correlationId?: string;
};

export class ApiError extends Error {
  readonly status: number;
  readonly problem?: ProblemDetails;

  constructor(status: number, problem?: ProblemDetails) {
    super(problem?.title ?? `Request failed with status ${status}`);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }
}

export function createApi(
  baseUrl: string,
  options: { getToken: () => string | undefined; onUnauthorized?: () => void },
): Api {
  const client = createClient<paths>({ baseUrl });
  const auth: Middleware = {
    onRequest({ request }) {
      const token = options.getToken();
      if (token) request.headers.set('Authorization', `Bearer ${token}`);
      return request;
    },
    onResponse({ response }) {
      if (response.status === 401) options.onUnauthorized?.();
      return response;
    },
  };
  client.use(auth);
  return client;
}

/** Returns the response body, or throws ApiError carrying the problem details. */
export async function unwrap<T>(request: Promise<{ data?: unknown; error?: unknown; response: Response }>): Promise<T> {
  const { data, error, response } = await request;
  if (!response.ok) throw new ApiError(response.status, (error ?? undefined) as ProblemDetails | undefined);
  return data as T;
}

export const ApiContext = createContext<Api | null>(null);

export function useApi(): Api {
  const api = useContext(ApiContext);
  if (!api) throw new Error('useApi must be used inside ApiContext');
  return api;
}
```

`web/src/shared/api/ApiProvider.tsx`:
```tsx
import { useMemo, type ReactNode } from 'react';
import { useSession } from '../auth/session';
import { useConfig } from '../config';
import { ApiContext, createApi } from './client';

export function ApiProvider({ children }: { children: ReactNode }) {
  const { apiBaseUrl } = useConfig();
  const session = useSession();
  const api = useMemo(
    () => createApi(apiBaseUrl, { getToken: session.getToken, onUnauthorized: session.signIn }),
    [apiBaseUrl, session],
  );
  return <ApiContext.Provider value={api}>{children}</ApiContext.Provider>;
}
```

`web/src/shared/auth/session.tsx`:
```tsx
import { createContext, useContext, useEffect, useMemo, useRef, type ReactNode } from 'react';
import { AuthProvider, useAuth } from 'react-oidc-context';
import type { AppConfig } from '../config';
import { FullPageMessage } from '../layout/FullPageMessage';

export type Session = {
  getToken: () => string | undefined;
  signIn: () => void;
  signOut: () => void;
};

export const SessionContext = createContext<Session | null>(null);

export function useSession(): Session {
  const session = useContext(SessionContext);
  if (!session) throw new Error('useSession must be used inside SessionProvider');
  return session;
}

/** Local dev: the API signs every request in as the configured dev user, so no token is needed. */
const developmentSession: Session = {
  getToken: () => undefined,
  signIn: () => window.location.reload(),
  signOut: () => window.location.assign('/'),
};

export function SessionProvider({ config, children }: { config: AppConfig; children: ReactNode }) {
  if (config.auth.mode === 'Development') {
    return <SessionContext.Provider value={developmentSession}>{children}</SessionContext.Provider>;
  }

  const origin = window.location.origin;
  return (
    <AuthProvider
      authority={config.auth.authority ?? ''}
      client_id={config.auth.clientId ?? ''}
      redirect_uri={`${origin}/`}
      scope="openid email profile"
      onSigninCallback={() => window.history.replaceState({}, document.title, window.location.pathname)}
    >
      <CognitoSession config={config}>{children}</CognitoSession>
    </AuthProvider>
  );
}

function CognitoSession({ config, children }: { config: AppConfig; children: ReactNode }) {
  const auth = useAuth();
  const token = useRef<string | undefined>(undefined);
  token.current = auth.user?.id_token;

  useEffect(() => {
    if (!auth.isLoading && !auth.isAuthenticated && !auth.activeNavigator && !auth.error) {
      void auth.signinRedirect();
    }
  }, [auth]);

  const session = useMemo<Session>(
    () => ({
      getToken: () => token.current,
      signIn: () => void auth.signinRedirect(),
      signOut: () => {
        void auth.removeUser();
        const logout = new URL('/logout', config.auth.logoutDomain ?? window.location.origin);
        logout.searchParams.set('client_id', config.auth.clientId ?? '');
        logout.searchParams.set('logout_uri', `${window.location.origin}/`);
        window.location.assign(logout.toString());
      },
    }),
    [auth, config.auth.clientId, config.auth.logoutDomain],
  );

  if (auth.error) return <FullPageMessage title="Sign-in failed" message={auth.error.message} />;
  if (!auth.isAuthenticated) return <FullPageMessage title="Signing you in…" loading />;
  return <SessionContext.Provider value={session}>{children}</SessionContext.Provider>;
}
```

`web/src/shared/me.ts`:
```ts
import { useQuery } from '@tanstack/react-query';
import { unwrap, useApi } from './api/client';
import type { paths } from './api/schema';
import type { DeepRequired } from './api/types';

export type Me = DeepRequired<paths['/api/me']['get']['responses'][200]['content']['application/json']>;

export function useMe() {
  const api = useApi();
  return useQuery({
    queryKey: ['me'],
    queryFn: () => unwrap<Me>(api.GET('/api/me')),
    staleTime: 5 * 60_000,
  });
}

/** For showing or hiding actions only; the API enforces every permission. */
export function usePermissions() {
  const roles = useMe().data?.roles ?? [];
  return {
    canWrite: roles.includes('Manager') || roles.includes('Admin'),
    canAdminister: roles.includes('Admin'),
  };
}
```

`web/src/shared/format.ts`:
```ts
const DASH = '—';

/** Date-only values (yyyy-MM-dd) are formatted as calendar dates, independent of the browser's time zone. */
export function formatDate(iso: string | null | undefined, culture: string): string {
  if (!iso) return DASH;
  const [year, month, day] = iso.split('-').map(Number);
  return new Intl.DateTimeFormat(culture, { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' }).format(
    new Date(Date.UTC(year, month - 1, day)),
  );
}

export function formatDateTime(iso: string | null | undefined, culture: string, timeZone: string): string {
  if (!iso) return DASH;
  return new Intl.DateTimeFormat(culture, { dateStyle: 'medium', timeStyle: 'short', timeZone }).format(new Date(iso));
}

export function formatMoney(amount: number | null | undefined, currency: string, culture: string): string {
  if (amount === null || amount === undefined) return DASH;
  const fractionDigits = Number.isInteger(amount) ? 0 : 2;
  return new Intl.NumberFormat(culture, {
    style: 'currency',
    currency,
    minimumFractionDigits: fractionDigits,
    maximumFractionDigits: fractionDigits,
  }).format(amount);
}
```

`web/src/shared/validation.ts`:
```ts
import type { ZodType } from 'zod';
import type { ProblemDetails } from './api/client';

/** Adapts a Zod schema to Mantine's form `validate` option; error keys are dotted paths such as "site.city". */
export function zodValidate<T>(schema: ZodType<unknown>) {
  return (values: T): Record<string, string> => {
    const result = schema.safeParse(values);
    if (result.success) return {};
    const errors: Record<string, string> = {};
    for (const issue of result.error.issues) {
      const path = issue.path.map(String).join('.');
      if (!(path in errors)) errors[path] = issue.message;
    }
    return errors;
  };
}

/** The API keys validation errors by camelCase dotted path, which matches Mantine's form paths. */
export function serverFieldErrors(problem?: ProblemDetails): Record<string, string> {
  return Object.fromEntries(Object.entries(problem?.errors ?? {}).map(([key, messages]) => [key, messages[0] ?? 'Invalid value']));
}
```

`web/src/shared/layout/FullPageMessage.tsx`:
```tsx
import { Center, Loader, Stack, Text, Title } from '@mantine/core';

export function FullPageMessage({ title, message, loading = false }: { title: string; message?: string; loading?: boolean }) {
  return (
    <Center mih="100vh" p="md">
      <Stack align="center" gap="xs" maw={480}>
        {loading && <Loader />}
        <Title order={3}>{title}</Title>
        {message && (
          <Text c="dimmed" ta="center">
            {message}
          </Text>
        )}
      </Stack>
    </Center>
  );
}
```

`web/src/shared/layout/NotFoundPage.tsx`:
```tsx
import { Button, Stack, Text, Title } from '@mantine/core';
import { Link } from 'react-router';

export function NotFoundPage({ message = "This page doesn't exist." }: { message?: string }) {
  return (
    <Stack align="flex-start">
      <Title order={2}>Not found</Title>
      <Text>{message}</Text>
      <Button component={Link} to="/projects" variant="light">
        Back to projects
      </Button>
    </Stack>
  );
}
```

`web/src/shared/layout/AppLayout.tsx`:
```tsx
import { Alert, AppShell, Avatar, Burger, Group, Image, Menu, NavLink, Text, Title, UnstyledButton } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { NavLink as RouterNavLink, Outlet } from 'react-router';
import { ApiError } from '../api/client';
import { useSession } from '../auth/session';
import { useConfig } from '../config';
import { useMe } from '../me';

function initials(name: string) {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0].toUpperCase())
    .join('');
}

export function AppLayout() {
  const config = useConfig();
  const session = useSession();
  const me = useMe();
  const [opened, { toggle, close }] = useDisclosure();
  const noGroup = me.error instanceof ApiError && me.error.status === 403;

  return (
    <AppShell header={{ height: 56 }} navbar={{ width: 220, breakpoint: 'sm', collapsed: { mobile: !opened } }} padding="md">
      <AppShell.Header>
        <Group h="100%" px="md" justify="space-between">
          <Group gap="sm">
            <Burger opened={opened} onClick={toggle} hiddenFrom="sm" size="sm" aria-label="Toggle navigation" />
            {config.logoUrl && <Image src={config.logoUrl} alt="" h={28} w="auto" />}
            <Title order={4}>{config.productName}</Title>
          </Group>
          {me.data && (
            <Menu position="bottom-end">
              <Menu.Target>
                <UnstyledButton aria-label="Account menu">
                  <Group gap="xs">
                    <Avatar size="sm" radius="xl">
                      {initials(me.data.name)}
                    </Avatar>
                    <Text size="sm">{me.data.name}</Text>
                  </Group>
                </UnstyledButton>
              </Menu.Target>
              <Menu.Dropdown>
                <Menu.Label>{me.data.roles.join(', ')}</Menu.Label>
                <Menu.Item onClick={session.signOut}>Sign out</Menu.Item>
              </Menu.Dropdown>
            </Menu>
          )}
        </Group>
      </AppShell.Header>
      <AppShell.Navbar p="xs">
        <NavLink component={RouterNavLink} to="/projects" label="Projects" onClick={close} />
      </AppShell.Navbar>
      <AppShell.Main>
        {noGroup ? (
          <Alert color="yellow" title="No access yet">
            Your account isn't in a PDS group. Ask an administrator to add you to Viewer, Manager or Admin.
          </Alert>
        ) : (
          <Outlet />
        )}
      </AppShell.Main>
    </AppShell>
  );
}
```

`web/src/features/portfolio/ProjectsPage.tsx` (placeholder until Task 12):
```tsx
import { Title } from '@mantine/core';

export function ProjectsPage() {
  return <Title order={2}>Projects</Title>;
}
```

`web/src/routes.tsx`:
```tsx
import { Navigate, type RouteObject } from 'react-router';
import { ProjectsPage } from './features/portfolio/ProjectsPage';
import { AppLayout } from './shared/layout/AppLayout';
import { NotFoundPage } from './shared/layout/NotFoundPage';

export const routes: RouteObject[] = [
  {
    path: '/',
    element: <AppLayout />,
    children: [
      { index: true, element: <Navigate to="/projects" replace /> },
      { path: 'projects', element: <ProjectsPage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
];
```

`web/src/App.tsx`:
```tsx
import { createTheme, DEFAULT_THEME, MantineProvider } from '@mantine/core';
import { Notifications } from '@mantine/notifications';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useState } from 'react';
import { createBrowserRouter, RouterProvider } from 'react-router';
import { routes } from './routes';
import { ApiError } from './shared/api/client';
import { ApiProvider } from './shared/api/ApiProvider';
import { SessionProvider } from './shared/auth/session';
import { ConfigContext, type AppConfig } from './shared/config';

export function App({ config }: { config: AppConfig }) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 30_000,
            retry: (count, error) => !(error instanceof ApiError && error.status < 500) && count < 2,
          },
        },
      }),
  );
  const [router] = useState(() => createBrowserRouter(routes));
  const primaryColor = config.primaryColor in DEFAULT_THEME.colors ? config.primaryColor : 'teal';

  return (
    <ConfigContext.Provider value={config}>
      <MantineProvider theme={createTheme({ primaryColor })}>
        <Notifications />
        <QueryClientProvider client={queryClient}>
          <SessionProvider config={config}>
            <ApiProvider>
              <RouterProvider router={router} />
            </ApiProvider>
          </SessionProvider>
        </QueryClientProvider>
      </MantineProvider>
    </ConfigContext.Provider>
  );
}
```

`web/src/main.tsx`:
```tsx
import '@mantine/core/styles.css';
import '@mantine/notifications/styles.css';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import { loadConfig } from './shared/config';

const root = document.getElementById('root');
if (!root) throw new Error('Missing #root element');

loadConfig()
  .then((config) => {
    document.title = config.productName;
    createRoot(root).render(
      <StrictMode>
        <App config={config} />
      </StrictMode>,
    );
  })
  .catch((error: unknown) => {
    root.textContent = `Could not load configuration: ${error instanceof Error ? error.message : String(error)}`;
  });
```

- [ ] **Step 4: Run tests, lint, typecheck and build**

Run: `cd web && npm test && npm run lint && npm run typecheck && npm run build`
Expected: all tests PASS (`format.test.ts` covers Review Focus 5); lint shows no errors; typecheck and build succeed.

- [ ] **Step 5: Check it in the browser against the local API**

With DynamoDB Local and the API running (Task 10), run `cd web && npm run dev` and open http://localhost:5173.
Expected: header shows "Property Development System" and "Dev User"; the Projects placeholder heading renders.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(web): app shell with config, session, typed API client and layout

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 12: Projects list page

**Files:**
- Create: `web/src/features/portfolio/types.ts`, `features/portfolio/api.ts`, `features/portfolio/StatusBadge.tsx`
- Modify: `web/src/features/portfolio/ProjectsPage.tsx` (replace the placeholder)
- Test: `web/src/features/portfolio/fixtures.ts`, `web/src/features/portfolio/ProjectsPage.test.tsx`

**Interfaces:**
- Consumes: `useApi`, `unwrap`, `DeepRequired`, `useConfig`, `usePermissions`, `formatDate`, `formatMoney` (Task 11).
- Produces: types `ProjectDetails`, `ProjectPage`, `ProjectListItem`, `ProjectInput`, `ProjectUpdateInput`, `ListQuery`, `Stage`, `Status`; constants `stages`, `statuses`, `stageLabel`, `statusLabel`, `statusColor`; hooks `useProjects(query)`, `useProject(id)`, `useCreateProject()`, `useUpdateProject(id)`, `useSetArchived(id)`; `<StatusBadge status>`; fixtures `sampleProject`, `pageOf(items)`.

- [ ] **Step 1: Write the failing tests**

`web/src/features/portfolio/fixtures.ts`:
```ts
import type { ProjectDetails, ProjectListItem, ProjectPage } from './types';

export const sampleProject: ProjectDetails = {
  id: '01923c5e-7b1a-7c3e-9a55-2f1d7a0b6c11',
  code: 'PDS-001',
  name: 'Harbour View Terraces',
  site: {
    addressLine: '12 Quay Street',
    suburb: 'Auckland Central',
    city: 'Auckland',
    region: 'Auckland',
    postcode: '1010',
    legalDescription: 'Lot 1 DP 12345',
    titleReference: 'NA123/45',
    landAreaSqm: 2450.5,
  },
  stage: 'Design',
  status: 'AtRisk',
  plannedStart: '2026-10-01',
  plannedCompletion: '2028-03-31',
  actualStart: null,
  actualCompletion: null,
  budgetAmount: 12500000,
  currency: 'NZD',
  projectManager: 'Alex Chen',
  description: 'Twelve terraced homes.',
  isArchived: false,
  createdAt: '2026-09-29T01:00:00Z',
  createdBy: 'Sam Taylor',
  updatedAt: '2026-09-29T01:00:00Z',
  updatedBy: 'Sam Taylor',
  version: 1,
};

export const sampleListItem: ProjectListItem = {
  id: sampleProject.id,
  code: sampleProject.code,
  name: sampleProject.name,
  stage: sampleProject.stage,
  status: sampleProject.status,
  suburb: 'Auckland Central',
  city: 'Auckland',
  plannedCompletion: sampleProject.plannedCompletion,
  budgetAmount: sampleProject.budgetAmount,
  currency: 'NZD',
  isArchived: false,
};

export const pageOf = (items: ProjectListItem[]): ProjectPage => ({ items, page: 1, pageSize: 25, totalCount: items.length });
```

`web/src/features/portfolio/ProjectsPage.test.tsx`:
```tsx
import { screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { API, renderApp } from '../../test/render';
import { server } from '../../test/server';
import { pageOf, sampleListItem } from './fixtures';

describe('ProjectsPage', () => {
  it('lists projects with status and formatted values', async () => {
    server.use(http.get(`${API}/api/portfolio/projects`, () => HttpResponse.json(pageOf([sampleListItem]))));

    renderApp('/projects');

    expect(await screen.findByRole('link', { name: 'PDS-001' })).toHaveAttribute('href', `/projects/${sampleListItem.id}`);
    expect(screen.getByText('At risk')).toBeInTheDocument();
    expect(screen.getByText('31 Mar 2028')).toBeInTheDocument();
    expect(screen.getByText('$12,500,000')).toBeInTheDocument();
  });

  it('sends the search term after typing', async () => {
    const searches: (string | null)[] = [];
    server.use(
      http.get(`${API}/api/portfolio/projects`, ({ request }) => {
        searches.push(new URL(request.url).searchParams.get('search'));
        return HttpResponse.json(pageOf([]));
      }),
    );

    const { user } = renderApp('/projects');
    await user.type(await screen.findByLabelText('Search'), 'harbour');

    await waitFor(() => expect(searches).toContain('harbour'));
  });

  it('explains when nothing matches', async () => {
    server.use(http.get(`${API}/api/portfolio/projects`, () => HttpResponse.json(pageOf([]))));

    renderApp('/projects');

    expect(await screen.findByText('No projects match these filters.')).toBeInTheDocument();
  });

  it('offers New project to managers', async () => {
    server.use(http.get(`${API}/api/portfolio/projects`, () => HttpResponse.json(pageOf([]))));

    renderApp('/projects', { roles: ['Manager'] });

    expect(await screen.findByRole('link', { name: 'New project' })).toBeInTheDocument();
  });

  it('hides New project and the archive switch from viewers', async () => {
    server.use(http.get(`${API}/api/portfolio/projects`, () => HttpResponse.json(pageOf([]))));

    renderApp('/projects', { roles: ['Viewer'] });
    await screen.findByText('Sam Taylor');

    expect(screen.queryByRole('link', { name: 'New project' })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Show archived')).not.toBeInTheDocument();
  });
});
```

Run: `cd web && npx vitest run src/features/portfolio`
Expected: FAIL (`./fixtures` imports `./types`, which does not exist).

- [ ] **Step 2: Implement types, hooks and the page**

`web/src/features/portfolio/types.ts`:
```ts
import type { paths } from '../../shared/api/schema';
import type { DeepRequired } from '../../shared/api/types';

type ProjectsPath = paths['/api/portfolio/projects'];
type ProjectPath = paths['/api/portfolio/projects/{id}'];

export type ProjectDetails = DeepRequired<ProjectPath['get']['responses'][200]['content']['application/json']>;
export type ProjectPage = DeepRequired<ProjectsPath['get']['responses'][200]['content']['application/json']>;
export type ProjectListItem = ProjectPage['items'][number];
export type ProjectInput = ProjectsPath['post']['requestBody']['content']['application/json'];
export type ProjectUpdateInput = ProjectPath['put']['requestBody']['content']['application/json'];
export type ListQuery = NonNullable<ProjectsPath['get']['parameters']['query']>;

export const stages = ['Acquisition', 'Feasibility', 'Design', 'Consenting', 'Construction', 'Sales', 'Completed'] as const;
export type Stage = (typeof stages)[number];

export const statuses = ['OnTrack', 'AtRisk', 'Delayed', 'OnHold', 'Cancelled'] as const;
export type Status = (typeof statuses)[number];

export const stageLabel: Record<Stage, string> = {
  Acquisition: 'Acquisition',
  Feasibility: 'Feasibility',
  Design: 'Design',
  Consenting: 'Consenting',
  Construction: 'Construction',
  Sales: 'Sales',
  Completed: 'Completed',
};

export const statusLabel: Record<Status, string> = {
  OnTrack: 'On track',
  AtRisk: 'At risk',
  Delayed: 'Delayed',
  OnHold: 'On hold',
  Cancelled: 'Cancelled',
};

export const statusColor: Record<Status, string> = {
  OnTrack: 'green',
  AtRisk: 'yellow',
  Delayed: 'red',
  OnHold: 'gray',
  Cancelled: 'dark',
};
```

`web/src/features/portfolio/api.ts`:
```ts
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { unwrap, useApi } from '../../shared/api/client';
import type { ListQuery, ProjectDetails, ProjectInput, ProjectPage, ProjectUpdateInput } from './types';

function useStoreProject() {
  const queryClient = useQueryClient();
  return (project: ProjectDetails) => {
    queryClient.setQueryData(['project', project.id], project);
    void queryClient.invalidateQueries({ queryKey: ['projects'] });
  };
}

export function useProjects(query: ListQuery) {
  const api = useApi();
  return useQuery({
    queryKey: ['projects', query],
    queryFn: () => unwrap<ProjectPage>(api.GET('/api/portfolio/projects', { params: { query } })),
    placeholderData: keepPreviousData,
  });
}

export function useProject(id: string) {
  const api = useApi();
  return useQuery({
    queryKey: ['project', id],
    queryFn: () => unwrap<ProjectDetails>(api.GET('/api/portfolio/projects/{id}', { params: { path: { id } } })),
  });
}

export function useCreateProject() {
  const api = useApi();
  const store = useStoreProject();
  return useMutation({
    mutationFn: (body: ProjectInput) => unwrap<ProjectDetails>(api.POST('/api/portfolio/projects', { body })),
    onSuccess: store,
  });
}

export function useUpdateProject(id: string) {
  const api = useApi();
  const store = useStoreProject();
  return useMutation({
    mutationFn: (body: ProjectUpdateInput) =>
      unwrap<ProjectDetails>(api.PUT('/api/portfolio/projects/{id}', { params: { path: { id } }, body })),
    onSuccess: store,
  });
}

export function useSetArchived(id: string) {
  const api = useApi();
  const store = useStoreProject();
  return useMutation({
    mutationFn: ({ archived, version }: { archived: boolean; version: number }) =>
      unwrap<ProjectDetails>(
        archived
          ? api.POST('/api/portfolio/projects/{id}/archive', { params: { path: { id } }, body: { version } })
          : api.POST('/api/portfolio/projects/{id}/restore', { params: { path: { id } }, body: { version } }),
      ),
    onSuccess: store,
  });
}
```

`web/src/features/portfolio/StatusBadge.tsx`:
```tsx
import { Badge } from '@mantine/core';
import { statusColor, statusLabel, type Status } from './types';

export function StatusBadge({ status }: { status: Status }) {
  return (
    <Badge color={statusColor[status]} variant="light">
      {statusLabel[status]}
    </Badge>
  );
}
```

Replace `web/src/features/portfolio/ProjectsPage.tsx` with:
```tsx
import { Alert, Anchor, Badge, Button, Group, Loader, Pagination, Select, Stack, Switch, Table, Text, TextInput, Title } from '@mantine/core';
import { useDebouncedValue } from '@mantine/hooks';
import { useState } from 'react';
import { Link } from 'react-router';
import { useConfig } from '../../shared/config';
import { formatDate, formatMoney } from '../../shared/format';
import { usePermissions } from '../../shared/me';
import { useProjects } from './api';
import { StatusBadge } from './StatusBadge';
import { stageLabel, stages, statuses, statusLabel, type Stage, type Status } from './types';

const PAGE_SIZE = 25;

export function ProjectsPage() {
  const config = useConfig();
  const { canWrite, canAdminister } = usePermissions();
  const [stage, setStage] = useState<Stage | null>(null);
  const [status, setStatus] = useState<Status | null>(null);
  const [search, setSearch] = useState('');
  const [includeArchived, setIncludeArchived] = useState(false);
  const [page, setPage] = useState(1);
  const [debouncedSearch] = useDebouncedValue(search.trim(), 300);

  const projects = useProjects({
    stage: stage ?? undefined,
    status: status ?? undefined,
    search: debouncedSearch || undefined,
    includeArchived: includeArchived || undefined,
    page,
    pageSize: PAGE_SIZE,
  });
  const data = projects.data;
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Projects</Title>
        {canWrite && (
          <Button component={Link} to="/projects/new">
            New project
          </Button>
        )}
      </Group>

      <Group align="flex-end" wrap="wrap">
        <TextInput
          label="Search"
          placeholder="Code, name, suburb or city"
          value={search}
          onChange={(event) => {
            setSearch(event.currentTarget.value);
            setPage(1);
          }}
        />
        <Select
          label="Stage"
          placeholder="All stages"
          clearable
          data={stages.map((value) => ({ value, label: stageLabel[value] }))}
          value={stage}
          onChange={(value) => {
            setStage(value as Stage | null);
            setPage(1);
          }}
        />
        <Select
          label="Status"
          placeholder="All statuses"
          clearable
          data={statuses.map((value) => ({ value, label: statusLabel[value] }))}
          value={status}
          onChange={(value) => {
            setStatus(value as Status | null);
            setPage(1);
          }}
        />
        {canAdminister && (
          <Switch
            label="Show archived"
            checked={includeArchived}
            onChange={(event) => {
              setIncludeArchived(event.currentTarget.checked);
              setPage(1);
            }}
          />
        )}
      </Group>

      {projects.error ? (
        <Alert color="red" title="Could not load projects">
          {projects.error.message}
        </Alert>
      ) : !data ? (
        <Loader />
      ) : data.items.length === 0 ? (
        <Text c="dimmed">No projects match these filters.</Text>
      ) : (
        <Table.ScrollContainer minWidth={760}>
          <Table striped highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Code</Table.Th>
                <Table.Th>Name</Table.Th>
                <Table.Th>Stage</Table.Th>
                <Table.Th>Status</Table.Th>
                <Table.Th>Location</Table.Th>
                <Table.Th>Planned completion</Table.Th>
                <Table.Th ta="right">Budget</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {data.items.map((project) => (
                <Table.Tr key={project.id}>
                  <Table.Td>
                    <Anchor component={Link} to={`/projects/${project.id}`}>
                      {project.code}
                    </Anchor>
                  </Table.Td>
                  <Table.Td>
                    {project.name}
                    {project.isArchived && (
                      <Badge ml="xs" color="gray" variant="outline">
                        Archived
                      </Badge>
                    )}
                  </Table.Td>
                  <Table.Td>{stageLabel[project.stage]}</Table.Td>
                  <Table.Td>
                    <StatusBadge status={project.status} />
                  </Table.Td>
                  <Table.Td>{[project.suburb, project.city].filter(Boolean).join(', ')}</Table.Td>
                  <Table.Td>{formatDate(project.plannedCompletion, config.culture)}</Table.Td>
                  <Table.Td ta="right" style={{ fontVariantNumeric: 'tabular-nums' }}>
                    {formatMoney(project.budgetAmount, project.currency, config.culture)}
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      )}

      {totalPages > 1 && <Pagination total={totalPages} value={page} onChange={setPage} />}
    </Stack>
  );
}
```

- [ ] **Step 3: Run tests, lint and typecheck**

Run: `cd web && npm test && npm run lint && npm run typecheck`
Expected: PASS. If typecheck reports that `project.stage` can't index `stageLabel`, the generated enum names differ: fix `types.ts` so `Stage`/`Status` equal the generated unions. Don't add casts in components.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat(web): projects list with search, filters and paging

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 13: Project form, create page and detail page

**Files:**
- Create: `web/src/features/portfolio/projectForm.ts`, `features/portfolio/ProjectForm.tsx`, `features/portfolio/ProjectCreatePage.tsx`, `features/portfolio/ProjectDetailPage.tsx`
- Modify: `web/src/routes.tsx` (add `projects/new` and `projects/:id`)
- Test: `web/src/features/portfolio/projectForm.test.ts`, `features/portfolio/ProjectCreatePage.test.tsx`, `features/portfolio/ProjectDetailPage.test.tsx`

**Interfaces:**
- Consumes: hooks and types from Task 12; `zodValidate`, `serverFieldErrors`, `ApiError`, formatting helpers (Task 11).
- Produces: `type ProjectFormValues`; `emptyProjectValues`; `projectSchema`; `toProjectInput(values) → ProjectInput`; `fromDetails(details) → ProjectFormValues`; `<ProjectForm initialValues submitLabel cancelTo onSubmit(values): Promise<void> onError(error)>`, which puts server 400 errors onto fields itself and passes every other error to `onError`; pages `ProjectCreatePage`, `ProjectDetailPage` (the detail page reserves an actions slot that Task 14 fills).

- [ ] **Step 1: Write the failing tests**

`web/src/features/portfolio/projectForm.test.ts`:
```ts
import { describe, expect, it } from 'vitest';
import { zodValidate } from '../../shared/validation';
import { sampleProject } from './fixtures';
import { emptyProjectValues, fromDetails, projectSchema, toProjectInput, type ProjectFormValues } from './projectForm';

const validate = zodValidate<ProjectFormValues>(projectSchema);
const valid = fromDetails(sampleProject);

describe('projectSchema', () => {
  it('accepts a complete project', () => {
    expect(validate(valid)).toEqual({});
  });

  it('reports every missing required field on an empty form', () => {
    const errors = validate(emptyProjectValues);

    expect(errors).toMatchObject({
      code: 'Code is required',
      name: 'Name is required',
      stage: 'Choose a stage',
      'site.addressLine': 'Address is required',
      'site.city': 'City is required',
    });
  });

  it('rejects completion before start on the completion field', () => {
    expect(validate({ ...valid, plannedStart: '2027-01-01', plannedCompletion: '2026-12-31' })).toEqual({
      plannedCompletion: 'Planned completion must be on or after the planned start',
    });
  });

  it('rejects budgets with more than two decimal places', () => {
    expect(validate({ ...valid, budgetAmount: 10.005 }).budgetAmount).toBe('Use at most 2 decimal places');
  });

  it('rejects codes with spaces', () => {
    expect(validate({ ...valid, code: 'PDS 001' }).code).toMatch(/letters, digits or hyphens/);
  });
});

describe('toProjectInput', () => {
  it('trims text and sends empty optional values as null', () => {
    const input = toProjectInput({ ...valid, name: '  Harbour  ', projectManager: '  ', budgetAmount: '', plannedStart: '' });

    expect(input.name).toBe('Harbour');
    expect(input.projectManager).toBeNull();
    expect(input.budgetAmount).toBeNull();
    expect(input.plannedStart).toBeNull();
  });

  it('round-trips details through the form values', () => {
    expect(toProjectInput(fromDetails(sampleProject))).toMatchObject({
      code: 'PDS-001',
      stage: 'Design',
      budgetAmount: 12500000,
      site: { city: 'Auckland', landAreaSqm: 2450.5 },
    });
  });
});
```

`web/src/features/portfolio/ProjectCreatePage.test.tsx`:
```tsx
import { screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { API, renderApp } from '../../test/render';
import { server } from '../../test/server';
import { sampleProject } from './fixtures';

async function fillRequiredFields(user: ReturnType<typeof renderApp>['user']) {
  await user.type(await screen.findByLabelText(/^Code/), 'pds-001');
  await user.type(screen.getByLabelText(/^Name/), 'Harbour View Terraces');
  await user.click(screen.getByLabelText(/^Stage/));
  await user.click(await screen.findByRole('option', { name: 'Design' }));
  await user.type(screen.getByLabelText(/^Address/), '12 Quay Street');
  await user.type(screen.getByLabelText(/^City/), 'Auckland');
}

describe('ProjectCreatePage', () => {
  it('shows field errors without calling the API when required fields are empty', async () => {
    let posts = 0;
    server.use(http.post(`${API}/api/portfolio/projects`, () => {
      posts += 1;
      return HttpResponse.json(sampleProject, { status: 201 });
    }));
    const { user } = renderApp('/projects/new');

    await user.click(await screen.findByRole('button', { name: 'Create project' }));

    expect(await screen.findByText('Code is required')).toBeInTheDocument();
    expect(screen.getByText('City is required')).toBeInTheDocument();
    expect(posts).toBe(0);
  });

  it('shows server validation errors next to the field', async () => {
    server.use(
      http.post(`${API}/api/portfolio/projects`, () =>
        HttpResponse.json(
          { title: 'One or more validation errors occurred.', status: 400, errors: { code: ['Code is already in use by another project.'] } },
          { status: 400 },
        ),
      ),
    );
    const { user } = renderApp('/projects/new');

    await fillRequiredFields(user);
    await user.click(screen.getByRole('button', { name: 'Create project' }));

    expect(await screen.findByText('Code is already in use by another project.')).toBeInTheDocument();
  });

  it('creates the project and opens it', async () => {
    let body: Record<string, unknown> | undefined;
    server.use(
      http.post(`${API}/api/portfolio/projects`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(sampleProject, { status: 201 });
      }),
      http.get(`${API}/api/portfolio/projects/${sampleProject.id}`, () => HttpResponse.json(sampleProject)),
    );
    const { user, router } = renderApp('/projects/new');

    await fillRequiredFields(user);
    await user.click(screen.getByRole('button', { name: 'Create project' }));

    expect(await screen.findByRole('heading', { name: /PDS-001/ })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe(`/projects/${sampleProject.id}`);
    expect(body).toMatchObject({ code: 'pds-001', stage: 'Design', status: 'OnTrack', budgetAmount: null });
  });
});
```

`web/src/features/portfolio/ProjectDetailPage.test.tsx`:
```tsx
import { screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { API, renderApp } from '../../test/render';
import { server } from '../../test/server';
import { sampleProject } from './fixtures';

describe('ProjectDetailPage', () => {
  it('shows the project with formatted dates and budget', async () => {
    server.use(http.get(`${API}/api/portfolio/projects/${sampleProject.id}`, () => HttpResponse.json(sampleProject)));

    renderApp(`/projects/${sampleProject.id}`);

    expect(await screen.findByRole('heading', { name: 'PDS-001 · Harbour View Terraces' })).toBeInTheDocument();
    expect(screen.getByText('At risk')).toBeInTheDocument();
    expect(screen.getByText('1 Oct 2026')).toBeInTheDocument();
    expect(screen.getByText('$12,500,000')).toBeInTheDocument();
    expect(screen.getByText('Lot 1 DP 12345')).toBeInTheDocument();
  });

  it('offers Edit to managers but not viewers', async () => {
    server.use(http.get(`${API}/api/portfolio/projects/${sampleProject.id}`, () => HttpResponse.json(sampleProject)));

    renderApp(`/projects/${sampleProject.id}`, { roles: ['Viewer'] });
    await screen.findByRole('heading', { name: /PDS-001/ });
    await screen.findByText('Sam Taylor');

    expect(screen.queryByRole('link', { name: 'Edit' })).not.toBeInTheDocument();
  });

  it('says so when the project does not exist', async () => {
    server.use(
      http.get(`${API}/api/portfolio/projects/${sampleProject.id}`, () =>
        HttpResponse.json({ title: 'Not Found', status: 404 }, { status: 404 }),
      ),
    );

    renderApp(`/projects/${sampleProject.id}`);

    expect(await screen.findByText("This project doesn't exist or was removed.")).toBeInTheDocument();
  });
});
```

Run: `cd web && npx vitest run src/features/portfolio`
Expected: FAIL (`./projectForm` not found).

- [ ] **Step 2: Implement the form model**

`web/src/features/portfolio/projectForm.ts`:
```ts
import { z } from 'zod';
import { stages, statuses, type ProjectDetails, type ProjectInput, type Stage, type Status } from './types';

export type ProjectFormValues = {
  code: string;
  name: string;
  stage: Stage | null;
  status: Status | null;
  plannedStart: string;
  plannedCompletion: string;
  actualStart: string;
  actualCompletion: string;
  budgetAmount: number | string;
  projectManager: string;
  description: string;
  site: {
    addressLine: string;
    suburb: string;
    city: string;
    region: string;
    postcode: string;
    legalDescription: string;
    titleReference: string;
    landAreaSqm: number | string;
  };
};

export const emptyProjectValues: ProjectFormValues = {
  code: '',
  name: '',
  stage: null,
  status: 'OnTrack',
  plannedStart: '',
  plannedCompletion: '',
  actualStart: '',
  actualCompletion: '',
  budgetAmount: '',
  projectManager: '',
  description: '',
  site: { addressLine: '', suburb: '', city: '', region: '', postcode: '', legalDescription: '', titleReference: '', landAreaSqm: '' },
};

const text = (max: number) => z.string().max(max, `Use ${max} characters or fewer`);
const required = (label: string, max: number) =>
  z.string().trim().min(1, `${label} is required`).max(max, `Use ${max} characters or fewer`);
const isoDate = z.string().refine((v) => v === '' || /^\d{4}-\d{2}-\d{2}$/.test(v), 'Enter a valid date');
const hasAtMostTwoDecimals = (v: number) => Math.abs(v * 100 - Math.round(v * 100)) < 1e-6;
const ordered = (start: string, end: string) => !start || !end || end >= start;

/** Mirrors the API validators (ProjectInputValidator) so most mistakes are caught before a round trip. */
export const projectSchema = z
  .object({
    code: z
      .string()
      .trim()
      .min(1, 'Code is required')
      .regex(/^[A-Za-z0-9][A-Za-z0-9-]{0,19}$/, 'Use up to 20 letters, digits or hyphens, starting with a letter or digit'),
    name: required('Name', 200),
    stage: z.enum(stages, { error: 'Choose a stage' }),
    status: z.enum(statuses, { error: 'Choose a status' }),
    plannedStart: isoDate,
    plannedCompletion: isoDate,
    actualStart: isoDate,
    actualCompletion: isoDate,
    budgetAmount: z.union([
      z.literal(''),
      z.number().min(0, 'Budget cannot be negative').refine(hasAtMostTwoDecimals, 'Use at most 2 decimal places'),
    ]),
    projectManager: text(200),
    description: text(4000),
    site: z.object({
      addressLine: required('Address', 200),
      suburb: text(100),
      city: required('City', 100),
      region: text(100),
      postcode: text(20),
      legalDescription: text(500),
      titleReference: text(100),
      landAreaSqm: z.union([z.literal(''), z.number().min(0, 'Land area cannot be negative')]),
    }),
  })
  .refine((v) => ordered(v.plannedStart, v.plannedCompletion), {
    path: ['plannedCompletion'],
    error: 'Planned completion must be on or after the planned start',
  })
  .refine((v) => ordered(v.actualStart, v.actualCompletion), {
    path: ['actualCompletion'],
    error: 'Actual completion must be on or after the actual start',
  });

const orNull = (value: string) => (value.trim() === '' ? null : value.trim());
const numberOrNull = (value: number | string) => (value === '' ? null : Number(value));

export function toProjectInput(v: ProjectFormValues): ProjectInput {
  return {
    code: v.code.trim(),
    name: v.name.trim(),
    stage: v.stage,
    status: v.status,
    plannedStart: orNull(v.plannedStart),
    plannedCompletion: orNull(v.plannedCompletion),
    actualStart: orNull(v.actualStart),
    actualCompletion: orNull(v.actualCompletion),
    budgetAmount: numberOrNull(v.budgetAmount),
    projectManager: orNull(v.projectManager),
    description: orNull(v.description),
    site: {
      addressLine: v.site.addressLine.trim(),
      suburb: orNull(v.site.suburb),
      city: v.site.city.trim(),
      region: orNull(v.site.region),
      postcode: orNull(v.site.postcode),
      legalDescription: orNull(v.site.legalDescription),
      titleReference: orNull(v.site.titleReference),
      landAreaSqm: numberOrNull(v.site.landAreaSqm),
    },
  };
}

export function fromDetails(d: ProjectDetails): ProjectFormValues {
  return {
    code: d.code,
    name: d.name,
    stage: d.stage,
    status: d.status,
    plannedStart: d.plannedStart ?? '',
    plannedCompletion: d.plannedCompletion ?? '',
    actualStart: d.actualStart ?? '',
    actualCompletion: d.actualCompletion ?? '',
    budgetAmount: d.budgetAmount ?? '',
    projectManager: d.projectManager ?? '',
    description: d.description ?? '',
    site: {
      addressLine: d.site.addressLine,
      suburb: d.site.suburb ?? '',
      city: d.site.city,
      region: d.site.region ?? '',
      postcode: d.site.postcode ?? '',
      legalDescription: d.site.legalDescription ?? '',
      titleReference: d.site.titleReference ?? '',
      landAreaSqm: d.site.landAreaSqm ?? '',
    },
  };
}
```

- [ ] **Step 3: Implement the form component and pages**

`web/src/features/portfolio/ProjectForm.tsx`:
```tsx
import { Button, Fieldset, Group, NumberInput, Select, SimpleGrid, Stack, Textarea, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { useState } from 'react';
import { Link } from 'react-router';
import { ApiError } from '../../shared/api/client';
import { useConfig } from '../../shared/config';
import { serverFieldErrors, zodValidate } from '../../shared/validation';
import { projectSchema, type ProjectFormValues } from './projectForm';
import { stageLabel, stages, statuses, statusLabel } from './types';

type Props = {
  initialValues: ProjectFormValues;
  submitLabel: string;
  cancelTo: string;
  onSubmit: (values: ProjectFormValues) => Promise<void>;
  onError: (error: unknown) => void;
};

const stageOptions = stages.map((value) => ({ value, label: stageLabel[value] }));
const statusOptions = statuses.map((value) => ({ value, label: statusLabel[value] }));

export function ProjectForm({ initialValues, submitLabel, cancelTo, onSubmit, onError }: Props) {
  const { currency } = useConfig();
  const [submitting, setSubmitting] = useState(false);
  const form = useForm<ProjectFormValues>({
    mode: 'controlled',
    initialValues,
    validate: zodValidate<ProjectFormValues>(projectSchema),
  });

  const handleSubmit = form.onSubmit(async (values) => {
    setSubmitting(true);
    try {
      await onSubmit(values);
    } catch (error) {
      if (error instanceof ApiError && error.status === 400 && error.problem?.errors) {
        form.setErrors(serverFieldErrors(error.problem));
      } else {
        onError(error);
      }
    } finally {
      setSubmitting(false);
    }
  });

  return (
    <form onSubmit={handleSubmit} noValidate>
      <Stack gap="lg" maw={880}>
        <Fieldset legend="Project">
          <SimpleGrid cols={{ base: 1, sm: 2 }}>
            <TextInput label="Code" description="Up to 20 letters, digits or hyphens" withAsterisk {...form.getInputProps('code')} />
            <TextInput label="Name" withAsterisk {...form.getInputProps('name')} />
            <Select label="Stage" withAsterisk data={stageOptions} {...form.getInputProps('stage')} />
            <Select label="Status" withAsterisk data={statusOptions} {...form.getInputProps('status')} />
          </SimpleGrid>
        </Fieldset>

        <Fieldset legend="Site">
          <SimpleGrid cols={{ base: 1, sm: 2 }}>
            <TextInput label="Address" withAsterisk {...form.getInputProps('site.addressLine')} />
            <TextInput label="Suburb" {...form.getInputProps('site.suburb')} />
            <TextInput label="City" withAsterisk {...form.getInputProps('site.city')} />
            <TextInput label="Region" {...form.getInputProps('site.region')} />
            <TextInput label="Postcode" {...form.getInputProps('site.postcode')} />
            <NumberInput label="Land area" suffix=" m²" min={0} thousandSeparator="," {...form.getInputProps('site.landAreaSqm')} />
            <TextInput label="Legal description" {...form.getInputProps('site.legalDescription')} />
            <TextInput label="Title reference" {...form.getInputProps('site.titleReference')} />
          </SimpleGrid>
        </Fieldset>

        <Fieldset legend="Timeline">
          <SimpleGrid cols={{ base: 1, sm: 2 }}>
            <TextInput type="date" label="Planned start" {...form.getInputProps('plannedStart')} />
            <TextInput type="date" label="Planned completion" {...form.getInputProps('plannedCompletion')} />
            <TextInput type="date" label="Actual start" {...form.getInputProps('actualStart')} />
            <TextInput type="date" label="Actual completion" {...form.getInputProps('actualCompletion')} />
          </SimpleGrid>
        </Fieldset>

        <Fieldset legend="Budget and people">
          <SimpleGrid cols={{ base: 1, sm: 2 }}>
            <NumberInput
              label={`Budget (${currency})`}
              prefix="$"
              min={0}
              decimalScale={2}
              thousandSeparator=","
              {...form.getInputProps('budgetAmount')}
            />
            <TextInput label="Project manager" {...form.getInputProps('projectManager')} />
          </SimpleGrid>
        </Fieldset>

        <Textarea label="Description" autosize minRows={3} {...form.getInputProps('description')} />

        <Group>
          <Button type="submit" loading={submitting}>
            {submitLabel}
          </Button>
          <Button component={Link} to={cancelTo} variant="default">
            Cancel
          </Button>
        </Group>
      </Stack>
    </form>
  );
}
```

`web/src/features/portfolio/ProjectCreatePage.tsx`:
```tsx
import { Stack, Title } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useNavigate } from 'react-router';
import { useCreateProject } from './api';
import { ProjectForm } from './ProjectForm';
import { emptyProjectValues, toProjectInput } from './projectForm';

export function ProjectCreatePage() {
  const create = useCreateProject();
  const navigate = useNavigate();

  return (
    <Stack>
      <Title order={2}>New project</Title>
      <ProjectForm
        initialValues={emptyProjectValues}
        submitLabel="Create project"
        cancelTo="/projects"
        onSubmit={async (values) => {
          const project = await create.mutateAsync(toProjectInput(values));
          notifications.show({ color: 'green', message: `Created ${project.code}` });
          await navigate(`/projects/${project.id}`);
        }}
        onError={(error) =>
          notifications.show({
            color: 'red',
            title: 'Could not create the project',
            message: error instanceof Error ? error.message : 'Unexpected error',
          })
        }
      />
    </Stack>
  );
}
```

`web/src/features/portfolio/ProjectDetailPage.tsx`:
```tsx
import { Alert, Badge, Button, Card, Group, Loader, SimpleGrid, Stack, Table, Text, Title } from '@mantine/core';
import type { ReactNode } from 'react';
import { Link, useParams } from 'react-router';
import { ApiError } from '../../shared/api/client';
import { useConfig } from '../../shared/config';
import { formatDate, formatDateTime, formatMoney } from '../../shared/format';
import { NotFoundPage } from '../../shared/layout/NotFoundPage';
import { usePermissions } from '../../shared/me';
import { useProject } from './api';
import { StatusBadge } from './StatusBadge';
import { stageLabel, type ProjectDetails } from './types';

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
        {label}
      </Text>
      <Text>{children || '—'}</Text>
    </div>
  );
}

/** Extra actions (archive / restore) are added in Task 14 through this slot. */
export function ProjectActions({ project }: { project: ProjectDetails }) {
  const { canWrite } = usePermissions();
  return (
    <Group>
      {canWrite && (
        <Button component={Link} to={`/projects/${project.id}/edit`} variant="light">
          Edit
        </Button>
      )}
    </Group>
  );
}

export function ProjectDetailPage() {
  const { id = '' } = useParams();
  const project = useProject(id);
  const config = useConfig();

  if (project.error instanceof ApiError && project.error.status === 404) {
    return <NotFoundPage message="This project doesn't exist or was removed." />;
  }
  if (project.error) {
    return (
      <Alert color="red" title="Could not load the project">
        {project.error.message}
      </Alert>
    );
  }
  if (!project.data) return <Loader />;

  const p = project.data;
  return (
    <Stack>
      <Group justify="space-between" align="flex-start">
        <Stack gap={4}>
          <Title order={2}>
            {p.code} · {p.name}
          </Title>
          <Group gap="xs">
            <StatusBadge status={p.status} />
            <Badge variant="outline">{stageLabel[p.stage]}</Badge>
            {p.isArchived && <Badge color="gray">Archived</Badge>}
          </Group>
        </Stack>
        <ProjectActions project={p} />
      </Group>

      <SimpleGrid cols={{ base: 1, md: 2 }}>
        <Card withBorder>
          <Title order={4} mb="sm">
            Site
          </Title>
          <SimpleGrid cols={2}>
            <Field label="Address">{p.site.addressLine}</Field>
            <Field label="Suburb">{p.site.suburb}</Field>
            <Field label="City">{p.site.city}</Field>
            <Field label="Region">{p.site.region}</Field>
            <Field label="Legal description">{p.site.legalDescription}</Field>
            <Field label="Title reference">{p.site.titleReference}</Field>
            <Field label="Land area">
              {p.site.landAreaSqm === null ? null : `${p.site.landAreaSqm.toLocaleString(config.culture)} m²`}
            </Field>
            <Field label="Postcode">{p.site.postcode}</Field>
          </SimpleGrid>
        </Card>

        <Card withBorder>
          <Title order={4} mb="sm">
            Timeline and budget
          </Title>
          <Table>
            <Table.Thead>
              <Table.Tr>
                <Table.Th />
                <Table.Th>Start</Table.Th>
                <Table.Th>Completion</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              <Table.Tr>
                <Table.Th>Planned</Table.Th>
                <Table.Td>{formatDate(p.plannedStart, config.culture)}</Table.Td>
                <Table.Td>{formatDate(p.plannedCompletion, config.culture)}</Table.Td>
              </Table.Tr>
              <Table.Tr>
                <Table.Th>Actual</Table.Th>
                <Table.Td>{formatDate(p.actualStart, config.culture)}</Table.Td>
                <Table.Td>{formatDate(p.actualCompletion, config.culture)}</Table.Td>
              </Table.Tr>
            </Table.Tbody>
          </Table>
          <SimpleGrid cols={2} mt="md">
            <Field label="Budget">{formatMoney(p.budgetAmount, p.currency, config.culture)}</Field>
            <Field label="Project manager">{p.projectManager}</Field>
          </SimpleGrid>
        </Card>
      </SimpleGrid>

      {p.description && (
        <Card withBorder>
          <Title order={4} mb="sm">
            Description
          </Title>
          <Text style={{ whiteSpace: 'pre-wrap' }}>{p.description}</Text>
        </Card>
      )}

      <Text size="sm" c="dimmed">
        Created by {p.createdBy} on {formatDateTime(p.createdAt, config.culture, config.timeZone)} · Last updated by{' '}
        {p.updatedBy} on {formatDateTime(p.updatedAt, config.culture, config.timeZone)}
      </Text>
    </Stack>
  );
}
```

In `web/src/routes.tsx`, import `ProjectCreatePage` and `ProjectDetailPage` and add these children after the `projects` route:
```tsx
      { path: 'projects/new', element: <ProjectCreatePage /> },
      { path: 'projects/:id', element: <ProjectDetailPage /> },
```

- [ ] **Step 4: Run tests, lint and typecheck**

Run: `cd web && npm test && npm run lint && npm run typecheck`
Expected: PASS.

- [ ] **Step 5: Try it against the local API**

With the API and `npm run dev` running, create a project at http://localhost:5173/projects/new, then create another with the same code in lower case.
Expected: the first opens its detail page; the second shows "Code is already in use by another project." under Code.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(web): project form, create page and detail page

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 14: Edit, archive and restore in the UI

**Files:**
- Create: `web/src/features/portfolio/ProjectEditPage.tsx`
- Modify: `web/src/features/portfolio/ProjectDetailPage.tsx` (`ProjectActions` gains Archive/Restore), `web/src/routes.tsx` (add `projects/:id/edit`)
- Test: `web/src/features/portfolio/ProjectEditPage.test.tsx`, `web/src/features/portfolio/ProjectActions.test.tsx`

**Interfaces:**
- Consumes: `useProject`, `useUpdateProject`, `useSetArchived`, `ProjectForm`, `fromDetails`, `toProjectInput` (Tasks 12–13).
- Produces: route `/projects/:id/edit`. On a 409 the edit page shows "This project was changed by someone else" with a **Reload latest** button that refetches and resets the form. On the detail page, Admins see Archive or Restore, which sends the current `version`.

- [ ] **Step 1: Write the failing tests**

`web/src/features/portfolio/ProjectEditPage.test.tsx`:
```tsx
import { screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { API, renderApp } from '../../test/render';
import { server } from '../../test/server';
import { sampleProject } from './fixtures';

const url = `${API}/api/portfolio/projects/${sampleProject.id}`;

describe('ProjectEditPage', () => {
  it('loads current values and saves with the version', async () => {
    let body: Record<string, unknown> | undefined;
    let current = sampleProject;
    server.use(
      http.get(url, () => HttpResponse.json(current)),
      http.put(url, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        current = { ...current, name: 'Renamed', version: 2 };
        return HttpResponse.json(current);
      }),
    );
    const { user, router } = renderApp(`/projects/${sampleProject.id}/edit`);

    const name = await screen.findByLabelText(/^Name/);
    expect(name).toHaveValue('Harbour View Terraces');
    await user.clear(name);
    await user.type(name, 'Renamed');
    await user.click(screen.getByRole('button', { name: 'Save changes' }));

    expect(await screen.findByRole('heading', { name: 'PDS-001 · Renamed' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe(`/projects/${sampleProject.id}`);
    expect(body).toMatchObject({ name: 'Renamed', version: 1 });
  });

  it('explains a conflict and reloads the latest version', async () => {
    let gets = 0;
    server.use(
      http.get(url, () => {
        gets += 1;
        return HttpResponse.json(gets === 1 ? sampleProject : { ...sampleProject, name: 'Changed elsewhere', version: 2 });
      }),
      http.put(url, () =>
        HttpResponse.json({ title: 'This project was changed by someone else.', status: 409 }, { status: 409 }),
      ),
    );
    const { user } = renderApp(`/projects/${sampleProject.id}/edit`);

    await screen.findByLabelText(/^Name/);
    await user.click(screen.getByRole('button', { name: 'Save changes' }));
    expect(await screen.findByText('This project was changed by someone else')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Reload latest' }));

    expect(await screen.findByDisplayValue('Changed elsewhere')).toBeInTheDocument();
    expect(screen.queryByText('This project was changed by someone else')).not.toBeInTheDocument();
  });
});
```

`web/src/features/portfolio/ProjectActions.test.tsx`:
```tsx
import { screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { API, renderApp } from '../../test/render';
import { server } from '../../test/server';
import { sampleProject } from './fixtures';

const url = `${API}/api/portfolio/projects/${sampleProject.id}`;

describe('ProjectActions', () => {
  it('lets admins archive with the current version', async () => {
    let body: unknown;
    server.use(
      http.get(url, () => HttpResponse.json(sampleProject)),
      http.post(`${url}/archive`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json({ ...sampleProject, isArchived: true, version: 2 });
      }),
    );
    const { user } = renderApp(`/projects/${sampleProject.id}`, { roles: ['Admin'] });

    await user.click(await screen.findByRole('button', { name: 'Archive' }));

    expect(await screen.findByRole('button', { name: 'Restore' })).toBeInTheDocument();
    expect(screen.getByText('Archived')).toBeInTheDocument();
    expect(body).toEqual({ version: 1 });
  });

  it('does not offer archive to managers', async () => {
    server.use(http.get(url, () => HttpResponse.json(sampleProject)));

    renderApp(`/projects/${sampleProject.id}`, { roles: ['Manager'] });

    expect(await screen.findByRole('link', { name: 'Edit' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Archive' })).not.toBeInTheDocument();
  });
});
```

Run: `cd web && npx vitest run src/features/portfolio`
Expected: FAIL (no edit route; no Archive button).

- [ ] **Step 2: Implement editing and archive actions**

`web/src/features/portfolio/ProjectEditPage.tsx`:
```tsx
import { Alert, Button, Loader, Stack, Text, Title } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useState } from 'react';
import { useNavigate, useParams } from 'react-router';
import { ApiError } from '../../shared/api/client';
import { useProject, useUpdateProject } from './api';
import { ProjectForm } from './ProjectForm';
import { fromDetails, toProjectInput } from './projectForm';

export function ProjectEditPage() {
  const { id = '' } = useParams();
  const project = useProject(id);
  const update = useUpdateProject(id);
  const navigate = useNavigate();
  const [conflict, setConflict] = useState(false);
  const [formGeneration, setFormGeneration] = useState(0);

  if (project.error) {
    return (
      <Alert color="red" title="Could not load the project">
        {project.error.message}
      </Alert>
    );
  }
  if (!project.data) return <Loader />;

  const current = project.data;
  return (
    <Stack>
      <Title order={2}>Edit {current.code}</Title>
      {conflict && (
        <Alert color="yellow" title="This project was changed by someone else">
          <Stack gap="xs" align="flex-start">
            <Text size="sm">Your changes were not saved. Reloading shows the latest version and discards your edits.</Text>
            <Button
              size="xs"
              variant="light"
              onClick={async () => {
                await project.refetch();
                setConflict(false);
                setFormGeneration((n) => n + 1);
              }}
            >
              Reload latest
            </Button>
          </Stack>
        </Alert>
      )}
      <ProjectForm
        key={`${current.id}-${current.version}-${formGeneration}`}
        initialValues={fromDetails(current)}
        submitLabel="Save changes"
        cancelTo={`/projects/${id}`}
        onSubmit={async (values) => {
          const saved = await update.mutateAsync({ ...toProjectInput(values), version: current.version });
          notifications.show({ color: 'green', message: `Saved ${saved.code}` });
          await navigate(`/projects/${id}`);
        }}
        onError={(error) => {
          if (error instanceof ApiError && error.status === 409) {
            setConflict(true);
            return;
          }
          notifications.show({
            color: 'red',
            title: 'Could not save the project',
            message: error instanceof Error ? error.message : 'Unexpected error',
          });
        }}
      />
    </Stack>
  );
}
```

In `web/src/features/portfolio/ProjectDetailPage.tsx`, replace the `ProjectActions` component with:
```tsx
export function ProjectActions({ project }: { project: ProjectDetails }) {
  const { canWrite, canAdminister } = usePermissions();
  const setArchived = useSetArchived(project.id);

  return (
    <Group>
      {canWrite && (
        <Button component={Link} to={`/projects/${project.id}/edit`} variant="light">
          Edit
        </Button>
      )}
      {canAdminister && (
        <Button
          variant="default"
          loading={setArchived.isPending}
          onClick={() =>
            setArchived.mutate(
              { archived: !project.isArchived, version: project.version },
              {
                onError: (error) =>
                  notifications.show({
                    color: 'red',
                    title:
                      error instanceof ApiError && error.status === 409
                        ? 'Someone else changed this project'
                        : 'Could not update the project',
                    message: 'Reload the page and try again.',
                  }),
              },
            )
          }
        >
          {project.isArchived ? 'Restore' : 'Archive'}
        </Button>
      )}
    </Group>
  );
}
```
In the same file, add `import { notifications } from '@mantine/notifications';` and change the `./api` import to `import { useProject, useSetArchived } from './api';`.

In `web/src/routes.tsx`, import `ProjectEditPage` and add after the `projects/:id` route:
```tsx
      { path: 'projects/:id/edit', element: <ProjectEditPage /> },
```

- [ ] **Step 3: Run tests, lint, typecheck and build**

Run: `cd web && npm test && npm run lint && npm run typecheck && npm run build`
Expected: PASS; `web/dist/index.html` exists.

- [ ] **Step 4: Try the conflict flow locally**

Open the same project's edit page in two browser tabs. Save in the first tab, then save in the second.
Expected: the second tab shows the conflict alert, and "Reload latest" shows the first tab's changes.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(web): edit with conflict handling, archive and restore

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 15: CDK app — deployment settings, data and auth stacks

**Files:**
- Create: `infra/cdk.json`, `infra/deployments/example-dev.json`
- Create: `infra/PDS.Infra/PDS.Infra.csproj`, `infra/PDS.Infra/Program.cs`, `infra/PDS.Infra/DeploymentSettings.cs`, `infra/PDS.Infra/PdsApp.cs`, `infra/PDS.Infra/DataStack.cs`, `infra/PDS.Infra/AuthStack.cs`
- Test: `tests/PDS.Infra.Tests/PDS.Infra.Tests.csproj`, `tests/PDS.Infra.Tests/TestDeployments.cs`, `tests/PDS.Infra.Tests/DataStackTests.cs`, `tests/PDS.Infra.Tests/AuthStackTests.cs`

**Interfaces:**
- Consumes: `PortfolioModule.Tables`, `TableKeys`, `Roles.All` (Tasks 1, 5).
- Produces: `record DeploymentSettings(string Name, string Account, string Region, bool IsProduction, string CognitoDomainPrefix, string? AlarmEmail, decimal MonthlyBudgetUsd, BrandingSettings Branding, LocaleSettings Locale, GitHubSettings? GitHub)` with `static Load(string path)`; `record AssetPaths(string Api, string Web)`; `PdsApp.Define(App, DeploymentSettings, AssetPaths, IReadOnlyList<TableDefinition>)`, which returns a tuple with named elements `Data` and `Auth` (Task 16 adds `App`); `DataStack.Tables: IReadOnlyDictionary<string, Table>` keyed by logical name; `AuthStack.UserPool`, `AuthStack.Domain`. Stack names are `<name>-data` and `<name>-auth`, and table names are `<name>-<logical>`.

- [ ] **Step 1: Create the infra project, its test project and example settings**

`infra/PDS.Infra/PDS.Infra.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <!-- jsii-generated CDK types raise deprecation warnings we cannot fix at the call site. -->
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Amazon.CDK.Lib" />
    <PackageReference Include="Constructs" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/PDS.Shared/PDS.Shared.csproj" />
    <ProjectReference Include="../../src/Modules/PDS.Portfolio/PDS.Portfolio.csproj" />
  </ItemGroup>
</Project>
```

`tests/PDS.Infra.Tests/PDS.Infra.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" PrivateAssets="all" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../infra/PDS.Infra/PDS.Infra.csproj" />
  </ItemGroup>
</Project>
```

`infra/cdk.json`:
```json
{
  "app": "dotnet run --project PDS.Infra",
  "context": {}
}
```

`infra/deployments/example-dev.json`:
```json
{
  "name": "example-dev",
  "account": "123456789012",
  "region": "ap-southeast-2",
  "isProduction": false,
  "cognitoDomainPrefix": "example-pds-dev",
  "alarmEmail": "ops@example.com",
  "monthlyBudgetUsd": 25,
  "branding": { "productName": "Property Development System", "logoUrl": null, "primaryColor": "teal" },
  "locale": { "currency": "NZD", "culture": "en-NZ", "timeZone": "Pacific/Auckland" },
  "gitHub": { "repository": "your-org/pds", "environment": "dev" }
}
```

Run: `dotnet sln PDS.slnx add infra/PDS.Infra/PDS.Infra.csproj tests/PDS.Infra.Tests/PDS.Infra.Tests.csproj`

- [ ] **Step 2: Write the failing tests**

`tests/PDS.Infra.Tests/TestDeployments.cs`:
```csharp
using PDS.Infra;

namespace PDS.Infra.Tests;

internal static class TestDeployments
{
    public static DeploymentSettings Settings(bool production) => new(
        Name: production ? "acme-prod" : "acme-dev",
        Account: "123456789012",
        Region: "ap-southeast-2",
        IsProduction: production,
        CognitoDomainPrefix: production ? "acme-pds" : "acme-pds-dev",
        AlarmEmail: "ops@example.com",
        MonthlyBudgetUsd: 50,
        Branding: new BrandingSettings("Property Development System", null, "teal"),
        Locale: new LocaleSettings("NZD", "en-NZ", "Pacific/Auckland"),
        GitHub: new GitHubSettings("example/pds", production ? "prod" : "dev"));

    /// <summary>Placeholder asset folders so synthesis works without a real build.</summary>
    public static AssetPaths Assets()
    {
        var root = Directory.CreateTempSubdirectory("pds-infra-");
        var api = Directory.CreateDirectory(Path.Combine(root.FullName, "api"));
        File.WriteAllText(Path.Combine(api.FullName, "PDS.Api.dll"), string.Empty);
        var web = Directory.CreateDirectory(Path.Combine(root.FullName, "web"));
        File.WriteAllText(Path.Combine(web.FullName, "index.html"), "<!doctype html>");
        return new AssetPaths(api.FullName, web.FullName);
    }
}
```

`tests/PDS.Infra.Tests/DataStackTests.cs`:
```csharp
using Amazon.CDK;
using Amazon.CDK.Assertions;
using PDS.Infra;
using PDS.Portfolio;

namespace PDS.Infra.Tests;

public class DataStackTests
{
    private static Template Synth(bool production) =>
        Template.FromStack(PdsApp.Define(new App(), TestDeployments.Settings(production), TestDeployments.Assets(), PortfolioModule.Tables).Data);

    [Fact]
    public void Creates_on_demand_table_with_keys_from_the_module_definition()
    {
        Synth(production: false).HasResourceProperties("AWS::DynamoDB::Table", Match.ObjectLike(new Dictionary<string, object>
        {
            ["TableName"] = "acme-dev-portfolio",
            ["BillingMode"] = "PAY_PER_REQUEST",
            ["KeySchema"] = new object[]
            {
                new Dictionary<string, object> { ["AttributeName"] = "pk", ["KeyType"] = "HASH" },
                new Dictionary<string, object> { ["AttributeName"] = "sk", ["KeyType"] = "RANGE" },
            },
            ["PointInTimeRecoverySpecification"] = new Dictionary<string, object> { ["PointInTimeRecoveryEnabled"] = true },
            ["GlobalSecondaryIndexes"] = new object[]
            {
                Match.ObjectLike(new Dictionary<string, object>
                {
                    ["IndexName"] = "gsi1",
                    ["Projection"] = new Dictionary<string, object> { ["ProjectionType"] = "ALL" },
                }),
            },
        }));
    }

    [Fact]
    public void Production_tables_are_retained_and_protected_from_deletion()
    {
        Synth(production: true).HasResource("AWS::DynamoDB::Table", new Dictionary<string, object>
        {
            ["DeletionPolicy"] = "Retain",
            ["Properties"] = Match.ObjectLike(new Dictionary<string, object> { ["DeletionProtectionEnabled"] = true }),
        });
    }
}
```

`tests/PDS.Infra.Tests/AuthStackTests.cs`:
```csharp
using Amazon.CDK;
using Amazon.CDK.Assertions;
using PDS.Infra;
using PDS.Portfolio;

namespace PDS.Infra.Tests;

public class AuthStackTests
{
    private static Template Synth() =>
        Template.FromStack(PdsApp.Define(new App(), TestDeployments.Settings(false), TestDeployments.Assets(), PortfolioModule.Tables).Auth);

    [Fact]
    public void Creates_one_group_per_role()
    {
        var template = Synth();

        template.ResourceCountIs("AWS::Cognito::UserPoolGroup", 3);
        foreach (var role in new[] { "Admin", "Manager", "Viewer" })
        {
            template.HasResourceProperties("AWS::Cognito::UserPoolGroup",
                Match.ObjectLike(new Dictionary<string, object> { ["GroupName"] = role }));
        }
    }

    [Fact]
    public void Only_administrators_can_create_users()
    {
        Synth().HasResourceProperties("AWS::Cognito::UserPool", Match.ObjectLike(new Dictionary<string, object>
        {
            ["AdminCreateUserConfig"] = Match.ObjectLike(new Dictionary<string, object> { ["AllowAdminCreateUserOnly"] = true }),
        }));
    }
}
```

Run: `dotnet test tests/PDS.Infra.Tests`
Expected: build FAILS (`PdsApp` not found).

- [ ] **Step 3: Implement settings, data and auth stacks**

`infra/PDS.Infra/DeploymentSettings.cs`:
```csharp
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PDS.Infra;

public sealed record BrandingSettings(string ProductName, string? LogoUrl, string PrimaryColor);

public sealed record LocaleSettings(string Currency, string Culture, string TimeZone);

/// <summary>The GitHub repository and environment allowed to assume this account's deploy role.</summary>
public sealed record GitHubSettings(string Repository, string Environment);

/// <summary>One file per deployment in infra/deployments/&lt;name&gt;.json. Client-specific values live only there.</summary>
public sealed partial record DeploymentSettings(
    string Name,
    string Account,
    string Region,
    bool IsProduction,
    string CognitoDomainPrefix,
    string? AlarmEmail,
    decimal MonthlyBudgetUsd,
    BrandingSettings Branding,
    LocaleSettings Locale,
    GitHubSettings? GitHub)
{
    public static DeploymentSettings Load(string path)
    {
        var settings = JsonSerializer.Deserialize<DeploymentSettings>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException($"{path} is empty.");
        if (!NamePattern().IsMatch(settings.Name))
            throw new InvalidOperationException($"Deployment name '{settings.Name}' must be lower-case letters, digits and hyphens.");
        return settings;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,30}$")]
    private static partial Regex NamePattern();
}

public sealed record AssetPaths(string Api, string Web);
```

`infra/PDS.Infra/DataStack.cs`:
```csharp
using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Constructs;
using PDS.Shared.Data;
using DynamoAttribute = Amazon.CDK.AWS.DynamoDB.Attribute;

namespace PDS.Infra;

/// <summary>One DynamoDB table per module definition, with the same keys the app creates locally.</summary>
public sealed class DataStack : Stack
{
    public DataStack(Construct scope, string id, IStackProps props, DeploymentSettings settings, IReadOnlyList<TableDefinition> definitions)
        : base(scope, id, props)
    {
        var tables = new Dictionary<string, Table>();
        foreach (var definition in definitions)
        {
            var table = new Table(this, $"{definition.LogicalName}-table", new TableProps
            {
                TableName = $"{settings.Name}-{definition.LogicalName}",
                PartitionKey = new DynamoAttribute { Name = TableKeys.PartitionKey, Type = AttributeType.STRING },
                SortKey = new DynamoAttribute { Name = TableKeys.SortKey, Type = AttributeType.STRING },
                BillingMode = BillingMode.PAY_PER_REQUEST,
                PointInTimeRecoverySpecification = new PointInTimeRecoverySpecification { PointInTimeRecoveryEnabled = true },
                DeletionProtection = settings.IsProduction,
                RemovalPolicy = settings.IsProduction ? RemovalPolicy.RETAIN : RemovalPolicy.DESTROY,
            });
            foreach (var index in definition.GlobalIndexes)
            {
                table.AddGlobalSecondaryIndex(new GlobalSecondaryIndexProps
                {
                    IndexName = index.Name,
                    PartitionKey = new DynamoAttribute { Name = index.PartitionKey, Type = AttributeType.STRING },
                    SortKey = new DynamoAttribute { Name = index.SortKey, Type = AttributeType.STRING },
                    ProjectionType = ProjectionType.ALL,
                });
            }

            tables[definition.LogicalName] = table;
        }

        Tables = tables;
    }

    public IReadOnlyDictionary<string, Table> Tables { get; }
}
```

`infra/PDS.Infra/AuthStack.cs`:
```csharp
using Amazon.CDK;
using Amazon.CDK.AWS.Cognito;
using Constructs;
using PDS.Shared.Security;

namespace PDS.Infra;

/// <summary>Cognito user pool with one group per PDS role. Users are created by an administrator.</summary>
public sealed class AuthStack : Stack
{
    public AuthStack(Construct scope, string id, IStackProps props, DeploymentSettings settings) : base(scope, id, props)
    {
        UserPool = new UserPool(this, "users", new UserPoolProps
        {
            UserPoolName = $"{settings.Name}-users",
            SelfSignUpEnabled = false,
            SignInAliases = new SignInAliases { Email = true },
            AutoVerify = new AutoVerifiedAttrs { Email = true },
            StandardAttributes = new StandardAttributes
            {
                Email = new StandardAttribute { Required = true, Mutable = true },
                Fullname = new StandardAttribute { Required = false, Mutable = true },
            },
            PasswordPolicy = new PasswordPolicy
            {
                MinLength = 12,
                RequireLowercase = true,
                RequireUppercase = true,
                RequireDigits = true,
                RequireSymbols = false,
            },
            AccountRecovery = AccountRecovery.EMAIL_ONLY,
            DeletionProtection = settings.IsProduction,
            RemovalPolicy = settings.IsProduction ? RemovalPolicy.RETAIN : RemovalPolicy.DESTROY,
        });

        foreach (var role in Roles.All)
        {
            _ = new CfnUserPoolGroup(this, $"group-{role.ToLowerInvariant()}", new CfnUserPoolGroupProps
            {
                UserPoolId = UserPool.UserPoolId,
                GroupName = role,
                Description = $"PDS {role}",
            });
        }

        Domain = UserPool.AddDomain("domain", new UserPoolDomainOptions
        {
            CognitoDomain = new CognitoDomainOptions { DomainPrefix = settings.CognitoDomainPrefix },
        });
    }

    public UserPool UserPool { get; }

    public UserPoolDomain Domain { get; }
}
```

`infra/PDS.Infra/PdsApp.cs`:
```csharp
using Amazon.CDK;
using PDS.Shared.Data;

namespace PDS.Infra;

public static class PdsApp
{
    public static (DataStack Data, AuthStack Auth) Define(
        App app, DeploymentSettings settings, AssetPaths assets, IReadOnlyList<TableDefinition> tables)
    {
        var props = new StackProps { Env = new Amazon.CDK.Environment { Account = settings.Account, Region = settings.Region } };
        var data = new DataStack(app, $"{settings.Name}-data", props, settings, tables);
        var auth = new AuthStack(app, $"{settings.Name}-auth", props, settings);
        Tags.Of(app).Add("pds:deployment", settings.Name);
        _ = assets; // used by the app stack (Task 16)
        return (data, auth);
    }
}
```

`infra/PDS.Infra/Program.cs`:
```csharp
using Amazon.CDK;
using PDS.Infra;
using PDS.Portfolio;

var app = new App();
var deployment = app.Node.TryGetContext("deployment") as string
    ?? throw new InvalidOperationException("Pass -c deployment=<name>; settings are read from deployments/<name>.json.");
var settings = DeploymentSettings.Load(Path.Combine("deployments", $"{deployment}.json"));
var assets = new AssetPaths(Api: Path.GetFullPath("../artifacts/api"), Web: Path.GetFullPath("../web/dist"));

PdsApp.Define(app, settings, assets, PortfolioModule.Tables);
app.Synth();
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/PDS.Infra.Tests`
Expected: PASS. The first run is slow while the jsii runtime starts; Node.js must be on PATH.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(infra): CDK deployment settings, DynamoDB and Cognito stacks

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 16: CDK app stack — Lambda API, HTTP API, SPA hosting, alarms and GitHub deploy role

**Files:**
- Create: `infra/PDS.Infra/AppStack.cs`, `infra/PDS.Infra/GitHubOidcStack.cs`
- Modify: `infra/PDS.Infra/PdsApp.cs` (replace whole file)
- Test: `tests/PDS.Infra.Tests/AppStackTests.cs`, `tests/PDS.Infra.Tests/GitHubOidcStackTests.cs`

**Interfaces:**
- Consumes: `DataStack.Tables`, `AuthStack.UserPool`, `AuthStack.Domain`, `AssetPaths`, `DeploymentSettings` (Task 15); `artifacts/api` (Task 10) and `web/dist` (Task 14) at synth time.
- Produces: the stack `<name>-app` with outputs `SiteUrl`, `ApiUrl` and `UserPoolId`, which `deploy-environment.yml` (Task 18) reads; the stack `<name>-github-oidc` with output `DeployRoleArn`; `PdsApp.Define` now returns `(Data, Auth, App)`. The Lambda environment carries `Auth__*`, `Branding__*`, `Locale__*` and `Tables__<logical>`.

- [ ] **Step 1: Write the failing tests**

`tests/PDS.Infra.Tests/AppStackTests.cs`:
```csharp
using Amazon.CDK;
using Amazon.CDK.Assertions;
using PDS.Infra;
using PDS.Portfolio;

namespace PDS.Infra.Tests;

public class AppStackTests
{
    private static Template Synth() =>
        Template.FromStack(PdsApp.Define(new App(), TestDeployments.Settings(false), TestDeployments.Assets(), PortfolioModule.Tables).App);

    [Fact]
    public void Api_runs_on_dotnet_10_arm64_outside_any_vpc()
    {
        Synth().HasResourceProperties("AWS::Lambda::Function", Match.ObjectLike(new Dictionary<string, object>
        {
            ["FunctionName"] = "acme-dev-api",
            ["Runtime"] = "dotnet10",
            ["Architectures"] = new object[] { "arm64" },
            ["Handler"] = "PDS.Api",
            ["VpcConfig"] = Match.Absent(),
        }));
    }

    [Fact]
    public void Api_is_configured_for_gateway_auth_and_its_table()
    {
        Synth().HasResourceProperties("AWS::Lambda::Function", Match.ObjectLike(new Dictionary<string, object>
        {
            ["FunctionName"] = "acme-dev-api",
            ["Environment"] = new Dictionary<string, object>
            {
                ["Variables"] = Match.ObjectLike(new Dictionary<string, object>
                {
                    ["ASPNETCORE_ENVIRONMENT"] = "Production",
                    ["Auth__Mode"] = "Gateway",
                    ["Locale__Currency"] = "NZD",
                    ["Tables__portfolio"] = Match.AnyValue(),
                }),
            },
        }));
    }

    [Fact]
    public void Config_route_is_public_and_everything_else_needs_a_jwt()
    {
        var template = Synth();

        template.HasResourceProperties("AWS::ApiGatewayV2::Route", Match.ObjectLike(new Dictionary<string, object>
        {
            ["RouteKey"] = "GET /api/config",
            ["AuthorizerId"] = Match.Absent(),
        }));
        template.HasResourceProperties("AWS::ApiGatewayV2::Route", Match.ObjectLike(new Dictionary<string, object>
        {
            ["RouteKey"] = "ANY /api/{proxy+}",
            ["AuthorizationType"] = "JWT",
        }));
    }

    [Fact]
    public void Nothing_creates_a_vpc_or_nat_gateway()
    {
        var template = Synth();

        template.ResourceCountIs("AWS::EC2::VPC", 0);
        template.ResourceCountIs("AWS::EC2::NatGateway", 0);
    }

    [Fact]
    public void Account_has_a_monthly_cost_budget() => Synth().ResourceCountIs("AWS::Budgets::Budget", 1);
}
```

`tests/PDS.Infra.Tests/GitHubOidcStackTests.cs`:
```csharp
using Amazon.CDK;
using Amazon.CDK.Assertions;
using PDS.Infra;

namespace PDS.Infra.Tests;

public class GitHubOidcStackTests
{
    [Fact]
    public void Deploy_role_trusts_only_the_configured_repository_environment()
    {
        var app = new App();
        var stack = new GitHubOidcStack(app, "oidc", new StackProps(), new GitHubSettings("example/pds", "prod"));

        Template.FromStack(stack).HasResourceProperties("AWS::IAM::Role", Match.ObjectLike(new Dictionary<string, object>
        {
            ["AssumeRolePolicyDocument"] = Match.ObjectLike(new Dictionary<string, object>
            {
                ["Statement"] = new object[]
                {
                    Match.ObjectLike(new Dictionary<string, object>
                    {
                        ["Condition"] = new Dictionary<string, object>
                        {
                            ["StringEquals"] = new Dictionary<string, object>
                            {
                                ["token.actions.githubusercontent.com:aud"] = "sts.amazonaws.com",
                                ["token.actions.githubusercontent.com:sub"] = "repo:example/pds:environment:prod",
                            },
                        },
                    }),
                },
            }),
        }));
    }
}
```

Run: `dotnet test tests/PDS.Infra.Tests`
Expected: build FAILS (`App` is not a member of the tuple returned by `Define`; `GitHubOidcStack` not found).

- [ ] **Step 2: Implement the app and GitHub stacks**

`infra/PDS.Infra/AppStack.cs`:
```csharp
using Amazon.CDK;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.Budgets;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.CloudWatch.Actions;
using Amazon.CDK.AWS.Cognito;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Deployment;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Amazon.CDK.AwsApigatewayv2Authorizers;
using Amazon.CDK.AwsApigatewayv2Integrations;
using Constructs;
using ApiHttpMethod = Amazon.CDK.AWS.Apigatewayv2.HttpMethod;
using LambdaFunction = Amazon.CDK.AWS.Lambda.Function;
using S3AssetOptions = Amazon.CDK.AWS.S3.Assets.AssetOptions;

namespace PDS.Infra;

/// <summary>SPA on S3 + CloudFront, API Lambda behind an HTTP API with a Cognito JWT authorizer, alarms and a budget.</summary>
public sealed class AppStack : Stack
{
    public AppStack(
        Construct scope,
        string id,
        IStackProps props,
        DeploymentSettings settings,
        AssetPaths assets,
        IReadOnlyDictionary<string, Table> tables,
        IUserPool userPool,
        UserPoolDomain domain)
        : base(scope, id, props)
    {
        // Static site
        var site = new Bucket(this, "site", new BucketProps
        {
            BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
            Encryption = BucketEncryption.S3_MANAGED,
            EnforceSSL = true,
            RemovalPolicy = RemovalPolicy.DESTROY,
            AutoDeleteObjects = true,
        });
        var cdn = new Distribution(this, "cdn", new DistributionProps
        {
            DefaultBehavior = new BehaviorOptions
            {
                Origin = S3BucketOrigin.WithOriginAccessControl(site),
                ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
            },
            DefaultRootObject = "index.html",
            ErrorResponses =
            [
                new ErrorResponse { HttpStatus = 403, ResponseHttpStatus = 200, ResponsePagePath = "/index.html", Ttl = Duration.Seconds(0) },
                new ErrorResponse { HttpStatus = 404, ResponseHttpStatus = 200, ResponsePagePath = "/index.html", Ttl = Duration.Seconds(0) },
            ],
        });
        var siteUrl = $"https://{cdn.DistributionDomainName}";

        // Sign-in client for the SPA (authorization code + PKCE, no secret)
        var client = new UserPoolClient(this, "spa-client", new UserPoolClientProps
        {
            UserPool = userPool,
            UserPoolClientName = $"{settings.Name}-spa",
            GenerateSecret = false,
            AuthFlows = new AuthFlow { UserSrp = true },
            OAuth = new OAuthSettings
            {
                Flows = new OAuthFlows { AuthorizationCodeGrant = true },
                Scopes = [OAuthScope.OPENID, OAuthScope.EMAIL, OAuthScope.PROFILE],
                CallbackUrls = [$"{siteUrl}/"],
                LogoutUrls = [$"{siteUrl}/"],
            },
            SupportedIdentityProviders = [UserPoolClientIdentityProvider.COGNITO],
            PreventUserExistenceErrors = true,
            IdTokenValidity = Duration.Hours(1),
            AccessTokenValidity = Duration.Hours(1),
            RefreshTokenValidity = Duration.Days(7),
        });
        var issuer = $"https://cognito-idp.{Region}.amazonaws.com/{userPool.UserPoolId}";

        // API function
        var environment = new Dictionary<string, string>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["Auth__Mode"] = "Gateway",
            ["Auth__Authority"] = issuer,
            ["Auth__ClientId"] = client.UserPoolClientId,
            ["Auth__LogoutDomain"] = domain.BaseUrl(),
            ["Branding__ProductName"] = settings.Branding.ProductName,
            ["Branding__PrimaryColor"] = settings.Branding.PrimaryColor,
            ["Locale__Currency"] = settings.Locale.Currency,
            ["Locale__Culture"] = settings.Locale.Culture,
            ["Locale__TimeZone"] = settings.Locale.TimeZone,
        };
        if (settings.Branding.LogoUrl is { } logoUrl)
            environment["Branding__LogoUrl"] = logoUrl;
        foreach (var (logicalName, table) in tables)
            environment[$"Tables__{logicalName}"] = table.TableName;

        var api = new LambdaFunction(this, "api", new FunctionProps
        {
            FunctionName = $"{settings.Name}-api",
            Runtime = Runtime.DOTNET_10,
            Architecture = Architecture.ARM_64,
            Handler = "PDS.Api",
            Code = Code.FromAsset(assets.Api),
            MemorySize = 1024,
            Timeout = Duration.Seconds(30),
            Environment = environment,
            LogGroup = new LogGroup(this, "api-logs", new LogGroupProps
            {
                LogGroupName = $"/aws/lambda/{settings.Name}-api",
                Retention = RetentionDays.THREE_MONTHS,
                RemovalPolicy = RemovalPolicy.DESTROY,
            }),
        });
        foreach (var table in tables.Values)
            table.GrantReadWriteData(api);

        // HTTP API: /api/config is public; everything else requires a Cognito ID token for this client
        var integration = new HttpLambdaIntegration("api-integration", api);
        var authorizer = new HttpJwtAuthorizer("cognito", issuer, new HttpJwtAuthorizerProps { JwtAudience = [client.UserPoolClientId] });
        var httpApi = new HttpApi(this, "http-api", new HttpApiProps
        {
            ApiName = $"{settings.Name}-api",
            CorsPreflight = new CorsPreflightOptions
            {
                AllowOrigins = [siteUrl],
                AllowMethods = [CorsHttpMethod.GET, CorsHttpMethod.POST, CorsHttpMethod.PUT, CorsHttpMethod.OPTIONS],
                AllowHeaders = ["authorization", "content-type", "x-correlation-id"],
                ExposeHeaders = ["location", "x-correlation-id"],
                MaxAge = Duration.Hours(1),
            },
        });
        httpApi.AddRoutes(new AddRoutesOptions { Path = "/api/config", Methods = [ApiHttpMethod.GET], Integration = integration });
        httpApi.AddRoutes(new AddRoutesOptions { Path = "/api/{proxy+}", Methods = [ApiHttpMethod.ANY], Integration = integration, Authorizer = authorizer });

        // SPA upload; config.json is generated per deployment so one build serves every client
        _ = new BucketDeployment(this, "site-deployment", new BucketDeploymentProps
        {
            DestinationBucket = site,
            Sources =
            [
                Source.Asset(assets.Web, new S3AssetOptions { Exclude = ["config.json"] }),
                Source.JsonData("config.json", new Dictionary<string, object> { ["apiBaseUrl"] = httpApi.ApiEndpoint }),
            ],
            Distribution = cdn,
            DistributionPaths = ["/*"],
        });

        // Alarms and budget
        var alarms = new Topic(this, "alarms", new TopicProps { TopicName = $"{settings.Name}-alarms" });
        if (settings.AlarmEmail is { } email)
            alarms.AddSubscription(new EmailSubscription(email));
        var notify = new SnsAction(alarms);
        var fiveMinutes = new MetricOptions { Period = Duration.Minutes(5), Statistic = "Sum" };

        void AddAlarm(string alarmId, IMetric metric, string description) =>
            new Alarm(this, alarmId, new AlarmProps
            {
                Metric = metric,
                Threshold = 1,
                EvaluationPeriods = 1,
                ComparisonOperator = ComparisonOperator.GREATER_THAN_OR_EQUAL_TO_THRESHOLD,
                TreatMissingData = TreatMissingData.NOT_BREACHING,
                AlarmDescription = description,
            }).AddAlarmAction(notify);

        AddAlarm("api-5xx", httpApi.MetricServerError(fiveMinutes), "The API returned 5xx responses.");
        AddAlarm("api-function-errors", api.MetricErrors(fiveMinutes), "The API function failed.");
        AddAlarm("api-function-throttles", api.MetricThrottles(fiveMinutes), "The API function was throttled.");

        _ = new CfnBudget(this, "monthly-budget", new CfnBudgetProps
        {
            Budget = new CfnBudget.BudgetDataProperty
            {
                BudgetName = $"{settings.Name}-monthly",
                BudgetType = "COST",
                TimeUnit = "MONTHLY",
                BudgetLimit = new CfnBudget.SpendProperty { Amount = (double)settings.MonthlyBudgetUsd, Unit = "USD" },
            },
            NotificationsWithSubscribers = settings.AlarmEmail is { } budgetEmail
                ? new object[]
                {
                    new CfnBudget.NotificationWithSubscribersProperty
                    {
                        Notification = new CfnBudget.NotificationProperty
                        {
                            NotificationType = "ACTUAL",
                            ComparisonOperator = "GREATER_THAN",
                            Threshold = 80,
                            ThresholdType = "PERCENTAGE",
                        },
                        Subscribers = new object[] { new CfnBudget.SubscriberProperty { SubscriptionType = "EMAIL", Address = budgetEmail } },
                    },
                }
                : null,
        });

        _ = new CfnOutput(this, "SiteUrl", new CfnOutputProps { Value = siteUrl });
        _ = new CfnOutput(this, "ApiUrl", new CfnOutputProps { Value = httpApi.ApiEndpoint });
        _ = new CfnOutput(this, "UserPoolId", new CfnOutputProps { Value = userPool.UserPoolId });
    }
}
```

`infra/PDS.Infra/GitHubOidcStack.cs`:
```csharp
using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Constructs;

namespace PDS.Infra;

/// <summary>
/// Lets one GitHub repository environment deploy to this account without stored keys. Deploy it once per account
/// with administrator credentials; the role only assumes the CDK bootstrap roles.
/// </summary>
public sealed class GitHubOidcStack : Stack
{
    public GitHubOidcStack(Construct scope, string id, IStackProps props, GitHubSettings github) : base(scope, id, props)
    {
        var provider = new OpenIdConnectProvider(this, "github", new OpenIdConnectProviderProps
        {
            Url = "https://token.actions.githubusercontent.com",
            ClientIds = ["sts.amazonaws.com"],
        });

        var role = new Role(this, "deploy-role", new RoleProps
        {
            RoleName = "pds-github-deploy",
            Description = "Assumed by GitHub Actions to run cdk deploy through the CDK bootstrap roles.",
            MaxSessionDuration = Duration.Hours(1),
            AssumedBy = new WebIdentityPrincipal(provider.OpenIdConnectProviderArn, new Dictionary<string, object>
            {
                ["StringEquals"] = new Dictionary<string, object>
                {
                    ["token.actions.githubusercontent.com:aud"] = "sts.amazonaws.com",
                    ["token.actions.githubusercontent.com:sub"] = $"repo:{github.Repository}:environment:{github.Environment}",
                },
            }),
        });
        role.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Actions = ["sts:AssumeRole"],
            Resources = [$"arn:aws:iam::{Account}:role/cdk-*"],
        }));

        _ = new CfnOutput(this, "DeployRoleArn", new CfnOutputProps { Value = role.RoleArn });
    }
}
```

Replace `infra/PDS.Infra/PdsApp.cs` with:
```csharp
using Amazon.CDK;
using PDS.Shared.Data;

namespace PDS.Infra;

public static class PdsApp
{
    /// <summary>
    /// Stacks for one deployment. The GitHub OIDC stack is defined when settings include GitHub, but CI deploys only
    /// the data, auth and app stacks; the OIDC stack is deployed by hand once per account.
    /// </summary>
    public static (DataStack Data, AuthStack Auth, AppStack App) Define(
        App app, DeploymentSettings settings, AssetPaths assets, IReadOnlyList<TableDefinition> tables)
    {
        var props = new StackProps { Env = new Amazon.CDK.Environment { Account = settings.Account, Region = settings.Region } };
        var data = new DataStack(app, $"{settings.Name}-data", props, settings, tables);
        var auth = new AuthStack(app, $"{settings.Name}-auth", props, settings);
        var web = new AppStack(app, $"{settings.Name}-app", props, settings, assets, data.Tables, auth.UserPool, auth.Domain);
        if (settings.GitHub is { } github)
            _ = new GitHubOidcStack(app, $"{settings.Name}-github-oidc", props, github);

        Tags.Of(app).Add("pds:deployment", settings.Name);
        return (data, auth, web);
    }
}
```

> If `Runtime.DOTNET_10` doesn't exist in the pinned CDK version, use `new Runtime("dotnet10", RuntimeFamily.DOTNET_CORE)`; the test asserts the CloudFormation value, which is the same either way. If the target region has no managed `dotnet10` Lambda runtime yet, stop and raise it. The fallback is a container-image function, which is a design change (spec §10, risk 1).

- [ ] **Step 3: Run tests to verify they pass**

Run: `dotnet test tests/PDS.Infra.Tests`
Expected: PASS.

- [ ] **Step 4: Synthesize the example deployment end to end**

Run:
```bash
dotnet publish src/PDS.Api -c Release -r linux-arm64 --self-contained false -p:PublishReadyToRun=true -o artifacts/api
(cd web && npm run build)
cd infra && npx --yes aws-cdk@2.1143.0 synth -c deployment=example-dev --quiet && ls cdk.out
```
Expected: synth succeeds; `cdk.out` contains `example-dev-data.template.json`, `example-dev-auth.template.json`, `example-dev-app.template.json` and `example-dev-github-oidc.template.json`.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(infra): API Lambda, HTTP API with JWT authorizer, SPA hosting, alarms, GitHub deploy role

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 17: Continuous integration

**Files:**
- Create: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: every build and test command from Tasks 1–16.
- Produces: a required check that fails on formatting drift, build warnings, a stale OpenAPI document or stale generated TypeScript, failing .NET, web or CDK tests, a lint or type error, or a failed CDK synth.

- [ ] **Step 1: Write the workflow**

`.github/workflows/ci.yml`:
```yaml
name: ci

on:
  pull_request:
  push:
    branches: [main]

permissions:
  contents: read

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v5

      - uses: actions/setup-dotnet@v5
        with:
          global-json-file: global.json

      - uses: actions/setup-node@v5
        with:
          node-version: 24
          cache: npm
          cache-dependency-path: web/package-lock.json

      - name: Restore
        run: dotnet restore

      - name: Check formatting
        run: dotnet format --verify-no-changes --no-restore

      - name: Build (regenerates the OpenAPI document)
        run: dotnet build --no-restore

      - name: OpenAPI document is committed and current
        run: git diff --exit-code -- web/src/shared/api/openapi.json

      - name: Test .NET (unit, architecture, integration, infra)
        run: dotnet test --no-build

      - name: Web install
        working-directory: web
        run: npm ci

      - name: Generated API types are committed and current
        working-directory: web
        run: npm run gen:api && git diff --exit-code -- src/shared/api/schema.d.ts

      - name: Web lint, typecheck, test and build
        working-directory: web
        run: |
          npm run lint
          npm run typecheck
          npm test
          npm run build

      - name: Package Lambda
        run: dotnet publish src/PDS.Api -c Release -r linux-arm64 --self-contained false -p:PublishReadyToRun=true -o artifacts/api

      - name: CDK synth
        working-directory: infra
        run: npx --yes aws-cdk@2.1143.0 synth -c deployment=example-dev --quiet
```

- [ ] **Step 2: Lint the workflow and run the same steps locally**

Run: `docker run --rm -v "$PWD:/repo" -w /repo rhysd/actionlint:latest`
Expected: no output (no problems).

Run:
```bash
dotnet format --verify-no-changes && dotnet build && git diff --exit-code -- web/src/shared/api/openapi.json && dotnet test
(cd web && npm ci && npm run gen:api && git diff --exit-code -- src/shared/api/schema.d.ts && npm run lint && npm run typecheck && npm test && npm run build)
```
Expected: every command exits 0.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "ci: build, test, lint, contract drift checks and CDK synth

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 18: Deployment workflows and first-deployment runbook

**Files:**
- Create: `.github/workflows/deploy.yml`, `.github/workflows/deploy-environment.yml`
- Modify: `README.md` (add a "Deploying" section)

**Interfaces:**
- Consumes: stack names `<name>-data`, `<name>-auth`, `<name>-app`, output `ApiUrl` (Task 16), deployment files in `infra/deployments/`.
- Produces: on each push to `main`, one build deploys to `dev`, passes a smoke test, then waits for approval before deploying the same build to `prod`. Each GitHub environment (`dev`, `prod`) needs the variables `AWS_DEPLOY_ROLE_ARN`, `AWS_REGION` and `PDS_DEPLOYMENT`, and `prod` needs required reviewers.

- [ ] **Step 1: Write the workflows**

`.github/workflows/deploy.yml`:
```yaml
name: deploy

on:
  push:
    branches: [main]
  workflow_dispatch:

concurrency:
  group: deploy
  cancel-in-progress: false

permissions:
  contents: read

jobs:
  package:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v5

      - uses: actions/setup-dotnet@v5
        with:
          global-json-file: global.json

      - uses: actions/setup-node@v5
        with:
          node-version: 24
          cache: npm
          cache-dependency-path: web/package-lock.json

      - name: Package Lambda
        run: dotnet publish src/PDS.Api -c Release -r linux-arm64 --self-contained false -p:PublishReadyToRun=true -o artifacts/api

      - name: Build SPA
        working-directory: web
        run: npm ci && npm run build

      - uses: actions/upload-artifact@v4
        with:
          name: bundle
          path: |
            artifacts/api
            web/dist
          if-no-files-found: error

  dev:
    needs: package
    uses: ./.github/workflows/deploy-environment.yml
    with:
      environment: dev
    permissions:
      contents: read
      id-token: write

  prod:
    needs: dev
    uses: ./.github/workflows/deploy-environment.yml
    with:
      environment: prod
    permissions:
      contents: read
      id-token: write
```

`.github/workflows/deploy-environment.yml`:
```yaml
name: deploy-environment

on:
  workflow_call:
    inputs:
      environment:
        type: string
        required: true

jobs:
  deploy:
    runs-on: ubuntu-latest
    environment: ${{ inputs.environment }}
    permissions:
      contents: read
      id-token: write
    steps:
      - uses: actions/checkout@v5

      - uses: actions/setup-dotnet@v5
        with:
          global-json-file: global.json

      - uses: actions/setup-node@v5
        with:
          node-version: 24

      - uses: actions/download-artifact@v4
        with:
          name: bundle
          path: .

      - uses: aws-actions/configure-aws-credentials@v5
        with:
          role-to-assume: ${{ vars.AWS_DEPLOY_ROLE_ARN }}
          aws-region: ${{ vars.AWS_REGION }}

      - name: Deploy stacks
        working-directory: infra
        env:
          DEPLOYMENT: ${{ vars.PDS_DEPLOYMENT }}
        run: >
          npx --yes aws-cdk@2.1143.0 deploy
          "$DEPLOYMENT-data" "$DEPLOYMENT-auth" "$DEPLOYMENT-app"
          -c deployment="$DEPLOYMENT"
          --require-approval never
          --outputs-file cdk-outputs.json

      - name: Smoke test
        env:
          DEPLOYMENT: ${{ vars.PDS_DEPLOYMENT }}
        run: |
          api_url=$(jq -r --arg stack "$DEPLOYMENT-app" '.[$stack].ApiUrl' infra/cdk-outputs.json)
          curl --fail --silent --show-error --retry 5 --retry-delay 5 "$api_url/api/config"
```

Run: `docker run --rm -v "$PWD:/repo" -w /repo rhysd/actionlint:latest`
Expected: no output.

- [ ] **Step 2: Add the deployment runbook to the README**

Append to `README.md`:
````markdown
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
````

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "ci: deploy to dev then prod with approval; first-deployment runbook

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## Spec coverage

| Spec section | Tasks |
|---|---|
| §1 Success criteria: sign in, list, filter, view, create, edit, archive, restore in `dev` | 3, 5–8, 11–14, 16, 18 |
| §1 Local run without AWS | 10, 11 |
| §1 CI on merge; prod behind approval with the same build | 17, 18 |
| §1 New module = copy shape + one line each | 5 (`PortfolioModule`), 9 (boundary tests), 15 (`PortfolioModule.Tables`) |
| §1 No client-specific values in source | Global Constraints, 2 (branding options), 15 (deployment files) |
| §3.3 Module rules | 9 |
| §3.4 Shared kernel | 1 |
| §4.1 Domain model and rules | 4, 5 (validators) |
| §4.2 DynamoDB table and access patterns | 4 (mapper), 5 (store), 6 (list), 7 (update/code swap), 9 (batch get) |
| §4.3 API | 5–8 |
| §4.4 Contracts | 9 |
| §5.1 Auth (gateway claims, group shapes, policies, dev mode refused outside Development) | 3, 16 (routes) |
| §5.2 Errors (400 by field, bad JSON, 404, 409, problem+json with correlationId) | 2, 5, 7 |
| §5.3 Configuration and tenant-readiness | 2, 3, 15, 16 |
| §5.4 Observability (Serilog JSON, correlation id, alarms) | 2, 16 |
| §6 Frontend | 11–14 |
| §7 Infrastructure | 15, 16 |
| §8 Testing strategy | every task; infra assertions in 15–16 |
| §9 CI/CD | 17, 18 |
| §10 Risks: runtime, region | 16 (runtime note), 18 (region step) |


