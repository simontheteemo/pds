# PDS Contractors & Contracts (v1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An organisation-wide contractor register with insurance and licence compliance, plus per-project contracts linking projects to contractors.

**Architecture:** A new module `src/Modules/PDS.Contractors` (Contracts: `ContractorId` and `ContractId`; everything else internal) owns the `contractors` table. It holds contractor items (`CONTRACTOR#id`/`PROFILE`, listed via `gsi1pk = CONTRACTOR`) and contract items (`PROJECT#pid`/`CONTRACT#id`, listed per contractor via `gsi1pk = CONTRACTOR#cid`). It reads projects only through `PDS.Portfolio.Contracts`. The web app adds `web/src/features/contractors`: list, detail and form pages, and a Contracts section on the project detail page.

**Tech Stack:** Same as the foundation and Risks.

**Spec:** `docs/superpowers/specs/2026-09-30-contractors-design.md`. Reference implementations to mirror: `src/Modules/PDS.Risks` (the most recent module, closest shape) and `src/Modules/PDS.Portfolio` (list-with-search, `ListProjects`; batch get, `ProjectStore.GetManyAsync`). On the web side, mirror `web/src/features/risks` (section and modal) and `web/src/features/portfolio` (list, detail and form pages).

## Global Constraints

- All foundation constraints apply: net10.0, central package versions, only Contracts and the Module class are public, `pk`/`sk`/`gsi1pk`/`gsi1sk` string keys, ProblemDetails, and camelCase JSON with string enums.
- **No new tests** (user directive). Verify with:
  - `dotnet format PDS.slnx`, then `dotnet build PDS.slnx` (0 warnings).
  - `npm run gen:api && npm run typecheck && npm run lint && npm run build` in web/.
  - `cdk synth`, and the manual checks each task names.
- Query parameters are plain handler parameters (not `[AsParameters]`), so generated names are camelCase.
- Compliance: an expiry date before today is Expired. A date from today up to and including today+30 is ExpiringSoon. The overall status is the worst of the recorded dates; with no dates recorded it's NotRecorded. "Today" is the date in the `Locale:TimeZone` time zone.
- Policies:
  - Reads need CanRead.
  - Creating and editing contractors and contracts needs CanWrite.
  - Deactivating and activating contractors needs CanAdminister.
- Commit messages end with the Co-Authored-By trailer your environment specifies.

## Review Focus

1. **Contract for an inactive or unknown contractor.** POST returns 400 on `contractorId`, never 500 and never a saved contract.
2. **Changing a contract's contractor.** A PUT with a different `contractorId` returns 400 on `contractorId`.
3. **Compliance boundaries** in NZ time:
   - expiry = today → ExpiringSoon (still valid today)
   - expiry = yesterday → Expired
   - expiry = today+30 → ExpiringSoon
   - expiry = today+31 → Current
4. **Archived or unknown project.** POST contract: archived → 400 on `projectId`; unknown → 404. Listing contracts for an unknown project → 404.
5. **Stale edits.** Contractor and contract updates, deactivate and activate all return 409 on a stale version. A no-op activate or deactivate returns 200 even when the version is stale (same rule as Risks close/reopen).

---

### Task 1: Contractors backend — module scaffold, contractor register, wiring

**Files:**
- Create: `src/Modules/PDS.Contractors/PDS.Contractors.csproj`, `ContractorsModule.cs`
- Create: `Contracts/ContractorId.cs`, `Contracts/ContractId.cs`
- Create: `Domain/AuditStamp.cs`, `Domain/ContractorType.cs`, `Domain/ComplianceStatus.cs`, `Domain/Compliance.cs`, `Domain/ContractorFields.cs`, `Domain/ContractorRules.cs`, `Domain/Contractor.cs`
- Create: `Data/ContractorsTable.cs`, `Data/ContractorItemMapper.cs`, `Data/ContractorStore.cs`
- Create: `Features/ContractorDtos.cs`, `Features/ContractorValidators.cs`, `Features/ContractorsProblems.cs`, `Features/LocalDate.cs`, `Features/ListContractors.cs`, `Features/GetContractor.cs`, `Features/CreateContractor.cs`, `Features/UpdateContractor.cs`, `Features/SetContractorActive.cs`
- Modify: `PDS.slnx`, `src/PDS.Api/PDS.Api.csproj`, `src/PDS.Api/Program.cs`, `infra/PDS.Infra/PDS.Infra.csproj`, `infra/PDS.Infra/Program.cs`
- Generated: `web/src/shared/api/openapi.json`, `web/src/shared/api/schema.d.ts`

**Interfaces:**
- Produces (public): `ContractorId`, `ContractId`, `ContractorsModule.{Tables, AddContractorsModule, MapContractorsEndpoints}`.
- Produces (internal, used by Task 2):
  - `ContractorsTable.{LogicalName="contractors", Gsi1="gsi1", Definition}`
  - `ContractorStore.{GetAsync(ContractorId), GetManyAsync(IReadOnlyCollection<ContractorId>), ListAllAsync(), CreateAsync(Contractor), UpdateAsync(Contractor, long expectedVersion) → bool}`
  - `Compliance.{For, Overall}`
  - `LocalDate.Today(TimeProvider, LocaleOptions) → DateOnly`
  - `ContractorsProblems.VersionConflict(string entity)`
  - `AuditStamp`
- HTTP operations: `ListContractors`, `GetContractor`, `CreateContractor`, `UpdateContractor`, `DeactivateContractor`, `ActivateContractor`, as in spec §4. `GET /api/contractors/{id}/contracts` comes in Task 2.

- [ ] **Step 1: Project and contracts**

`PDS.Contractors.csproj` is identical to `PDS.Risks.csproj` (FrameworkReference ASP.NET Core; FluentValidation.DependencyInjectionExtensions; ProjectReferences to PDS.Shared and PDS.Portfolio). Add it to `PDS.slnx`.

`Contracts/ContractorId.cs` and `Contracts/ContractId.cs`: `public readonly record struct ContractorId(Guid Value)` and `ContractId(Guid Value)`, each with `static New() => new(Guid.CreateVersion7())` and `ToString() => Value.ToString()`. This is the same shape as `RiskId`.

- [ ] **Step 2: Domain**

```csharp
// Domain/AuditStamp.cs
namespace PDS.Contractors.Domain;
internal readonly record struct AuditStamp(DateTimeOffset At, string By);

// Domain/ContractorType.cs
namespace PDS.Contractors.Domain;
internal enum ContractorType { Contractor, Subcontractor, Consultant, Supplier }

// Domain/ComplianceStatus.cs
namespace PDS.Contractors.Domain;
internal enum ComplianceStatus { Current, ExpiringSoon, Expired, NotRecorded }
```

`Domain/Compliance.cs`:
```csharp
namespace PDS.Contractors.Domain;

internal static class Compliance
{
    public const int WarningDays = 30;

    /// <summary>Expired before today; ExpiringSoon from today to today+30 inclusive; otherwise Current.</summary>
    public static ComplianceStatus For(DateOnly? expiry, DateOnly today) => expiry switch
    {
        null => ComplianceStatus.NotRecorded,
        { } d when d < today => ComplianceStatus.Expired,
        { } d when d <= today.AddDays(WarningDays) => ComplianceStatus.ExpiringSoon,
        _ => ComplianceStatus.Current,
    };

    /// <summary>The worst status among the recorded dates; NotRecorded when none are recorded.</summary>
    public static ComplianceStatus Overall(IEnumerable<DateOnly?> expiries, DateOnly today)
    {
        var statuses = expiries.Where(d => d is not null).Select(d => For(d, today)).ToList();
        if (statuses.Count == 0)
            return ComplianceStatus.NotRecorded;
        if (statuses.Contains(ComplianceStatus.Expired))
            return ComplianceStatus.Expired;
        return statuses.Contains(ComplianceStatus.ExpiringSoon) ? ComplianceStatus.ExpiringSoon : ComplianceStatus.Current;
    }
}
```

`Domain/ContractorFields.cs`:
```csharp
namespace PDS.Contractors.Domain;

internal sealed record ContractorFields(
    string Name,
    ContractorType Type,
    string? Trade,
    string? Nzbn,
    string? ContactName,
    string? Email,
    string? Phone,
    string? Address,
    DateOnly? PublicLiabilityExpiry,
    DateOnly? ProfessionalIndemnityExpiry,
    string? LicenceNumber,
    DateOnly? LicenceExpiry,
    string? Notes)
{
    public IEnumerable<DateOnly?> Expiries => [PublicLiabilityExpiry, ProfessionalIndemnityExpiry, LicenceExpiry];
}
```

`Domain/ContractorRules.cs` holds the constants:
- `NameMaxLength = 200`, `TradeMaxLength = 100`, `ContactMaxLength = 200`, `EmailMaxLength = 200`, `PhoneMaxLength = 50`, `AddressMaxLength = 300`, `LicenceMaxLength = 50`, `NotesMaxLength = 4000`
- `[GeneratedRegex("^[0-9]{13}$")] NzbnPattern()`

It also has `Normalise(ContractorFields)`: trim `Name`; every other string becomes `Clean` (null when blank, otherwise trimmed); `Email` is also lower-cased.

Finally, `EnsureValid(ContractorFields)`: throws when Name is empty, or when Nzbn is present and doesn't match the pattern.

`Domain/Contractor.cs`: like `Risk`.
- Private constructor.
- Properties: `Id` (ContractorId), `Fields`, `IsActive`, `Created`, `Updated`, `Version`.
- `static Create(fields, stamp)`: normalise, validate, active, version 1.
- `static Rehydrate(...)`.
- `Update(fields, stamp)`: normalise, validate, touch.
- `bool SetActive(bool active, AuditStamp stamp)`: false when there's no change.
- `ComplianceStatus ComplianceOn(DateOnly today) => Compliance.Overall(Fields.Expiries, today)`.

- [ ] **Step 3: Data**

`Data/ContractorsTable.cs`: `LogicalName = "contractors"`, `Gsi1 = "gsi1"`, `Definition = new(LogicalName, [new GlobalIndex(Gsi1, "gsi1pk", "gsi1sk")])`.

`Data/ContractorItemMapper.cs` (mirror `RiskItemMapper`):
- **Keys:** `ContractorType = "CONTRACTOR"`. `Key(ContractorId id)` → pk `CONTRACTOR#{id}`, sk `PROFILE`.
- **`ToItem`** sets:
  - `gsi1pk = "CONTRACTOR"` and `gsi1sk = $"{Name.ToLowerInvariant()}#{Id}"`
  - `id`, `name`, `type`
  - optional strings and dates via `Attr.SetIfPresent`
  - `isActive` (Bool), the audit attributes, and `version`
- **`FromItem`** is the inverse. Missing optional attributes become null, and a missing `isActive` means **true**, for tolerant reads.

`Data/ContractorStore.cs`:
```csharp
internal sealed class ContractorStore(IAmazonDynamoDB dynamo, TableNames names)
{
    private string Table => names.For(ContractorsTable.LogicalName);

    public async Task<Contractor?> GetAsync(ContractorId id, CancellationToken ct) { /* GetItem, ConsistentRead, mapper */ }

    /// <summary>All contractors via gsi1pk = CONTRACTOR (all pages).</summary>
    public async Task<IReadOnlyList<Contractor>> ListAllAsync(CancellationToken ct) { /* Query IndexName=gsi1, KeyCondition "gsi1pk = :type", loop LastEvaluatedKey */ }

    /// <summary>BatchGetItem in chunks of 100 with Distinct() and an UnprocessedKeys loop (mirror ProjectStore.GetManyAsync).</summary>
    public async Task<IReadOnlyList<Contractor>> GetManyAsync(IReadOnlyCollection<ContractorId> ids, CancellationToken ct) { ... }

    public Task CreateAsync(Contractor c, CancellationToken ct) { /* PutItem attribute_not_exists(pk) */ }

    public async Task<bool> UpdateAsync(Contractor c, long expectedVersion, CancellationToken ct) { /* PutItem "#version = :expected"; false on ConditionalCheckFailedException */ }
}
```
Each body is a direct transcription of the corresponding `RiskStore` / `ProjectStore` method with the contractor mapper and keys.

- [ ] **Step 4: Features**

`Features/LocalDate.cs`:
```csharp
using PDS.Shared.Settings;

namespace PDS.Contractors.Features;

internal static class LocalDate
{
    /// <summary>Today's date in the deployment time zone (Locale:TimeZone, e.g. Pacific/Auckland).</summary>
    public static DateOnly Today(TimeProvider clock, LocaleOptions locale)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(locale.TimeZone);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
    }
}
```

`Features/ContractorDtos.cs`:
- **`IContractorInput`** has every `ContractorFields` property, with `Type` as `ContractorType?`.
- **`CreateContractorRequest(...)`** and **`UpdateContractorRequest(..., long Version)`** implement it.
- **`SetActiveRequest(long Version)`**.
- **`ToFields()`** (call only after validation).
- **`ContractorListItem(Guid Id, string Name, ContractorType Type, string? Trade, ComplianceStatus Compliance, bool IsActive)`**.
- **`ContractorDetails`** has every field plus:
  - `ComplianceStatus Compliance`
  - `ComplianceStatus PublicLiabilityStatus`, `ProfessionalIndemnityStatus`, `LicenceStatus`
  - `bool IsActive`, the audit fields and `long Version`
  - `static From(Contractor, DateOnly today)`

`Features/ContractorValidators.cs` (mirror `RiskValidators`):
- Name: required, ≤ 200.
- Type: NotNull and IsInEnum.
- Trade, ContactName, Phone, Address, LicenceNumber, Notes: max lengths as in the rules.
- Email: `.EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))`, ≤ 200.
- Nzbn: `.Matches("^[0-9]{13}$").When(not blank)`, with message "NZBN is 13 digits."
- Update: Version > 0.
- SetActiveRequest: Version > 0.

`Features/ContractorsProblems.cs`:
- `VersionConflict(string entity)` returns 409 "This {entity} was changed by someone else." with the same detail style as Risks.
- `ProjectArchived()` returns 400 on `projectId`.
- `ContractorUnavailable()` returns 400 on `contractorId` with "Choose an active contractor."
- `ContractorChanged()` returns 400 on `contractorId` with "A contract's contractor can't be changed."
- `InvalidType()` returns 400 on `type` with "Choose a valid contractor type."

Endpoints go in a group `/api/contractors` (WithTags "Contractors", RequireAuthorization CanRead), one file each, following the Risks endpoint files:
- **`ListContractors`**: `GET ""`, params `(string? search, ContractorType? type, bool? includeInactive, ContractorStore, TimeProvider, IOptions<LocaleOptions>)`.
  - If `type` is set but `!Enum.IsDefined(type.Value)`, return `InvalidType()`.
  - Filter: active unless `includeInactive`; type match; search is a case-insensitive `Contains` on Name, Trade and ContactName.
  - Order by Name (OrdinalIgnoreCase), map to list items with `Compliance = ComplianceOn(today)`, and return `Ok(List<ContractorListItem>)`.
  - Return type: `Results<Ok<List<ContractorListItem>>, ValidationProblem>`.
- **`GetContractor`**: `GET "/{contractorId:guid}"`, returns `Ok(ContractorDetails)` or NotFound.
- **`CreateContractor`**: `POST ""` with CanWrite and validation. Create, then `Created($"/api/contractors/{id}", details)`.
- **`UpdateContractor`**: `PUT "/{contractorId:guid}"` with CanWrite and validation.
  - Load: NotFound if missing.
  - Version mismatch → 409.
  - Update, then conditional write; a failed write → 409.
- **`SetContractorActive`**: `POST "/{contractorId:guid}/deactivate"` and `"/activate"`, CanAdminister, validated `SetActiveRequest`.
  - Load: NotFound if missing.
  - **No-op check first** (already in the requested state → Ok unchanged).
  - Then the version check → 409, then `SetActive` and a conditional write.
  - Operation names: `DeactivateContractor` and `ActivateContractor`.

`ContractorsModule.cs` (mirror `RisksModule`):
- `Tables = [ContractorsTable.Definition]`.
- `AddContractorsModule`: register the definition and `ContractorStore` as singletons, and add validators with `includeInternalTypes: true`.
- `MapContractorsEndpoints`: map the contractors group now; Task 2 adds the contracts group here.

Wiring:
- `PDS.Api.csproj` gets a ProjectReference.
- `Program.cs`: `using PDS.Contractors;`, `builder.Services.AddContractorsModule();`, `app.MapContractorsEndpoints();`, each after the Risks equivalent.
- `PDS.Infra.csproj` gets a ProjectReference.
- Infra `Program.cs`: `[.. PortfolioModule.Tables, .. RisksModule.Tables, .. ContractorsModule.Tables]`.

- [ ] **Step 5: Build, contract, manual check, commit**

- Run `dotnet format PDS.slnx && dotnet build PDS.slnx` (0 warnings), then `npm run gen:api` in web/.
- **Manual check** (DynamoDB Local plus the API in Development; the dev user is Admin):
  - Create a contractor with a public liability expiry of yesterday: compliance is Expired.
  - Create another with no dates (NotRecorded), then edit it to add a licence expiry of today+10: ExpiringSoon.
  - List it with `search=` on part of the trade name.
  - `type=Supplier` filters the list.
  - `type=99` returns 400.
  - Create with an invalid NZBN "123": 400 on `nzbn`.
  - Deactivate: the default list hides it and `includeInactive=true` shows it.
  - Deactivate again with a stale version: 200.
  - Activate, then update with a stale version: 409.
- **Synth:** `cdk synth` shows an `example-dev-contractors` table with gsi1 and `Tables__contractors`.
- **Commit:** `feat(contractors): contractor register module and API`.

---

### Task 2: Contracts backend

**Files:**
- Create: `Domain/ContractForm.cs`, `Domain/ContractStatus.cs`, `Domain/ContractFields.cs`, `Domain/ContractRules.cs`, `Domain/Contract.cs`
- Create: `Data/ContractItemMapper.cs`, `Data/ContractStore.cs`
- Create: `Features/ContractDtos.cs`, `Features/ContractValidators.cs`, `Features/ProjectLookup.cs`, `Features/ListProjectContracts.cs`, `Features/ListContractorContracts.cs`, `Features/GetContract.cs`, `Features/CreateContract.cs`, `Features/UpdateContract.cs`
- Modify: `ContractorsModule.cs` (register `ContractStore`, map the contracts group and the contractor-contracts route)
- Generated: `openapi.json`, `schema.d.ts`

**Interfaces:**
- Consumes (Task 1): `ContractorStore`, `ContractorsTable`, `ContractorsProblems`, `AuditStamp`; `IPortfolioQueries` and `ProjectSummary` (Portfolio contracts); `LocaleOptions` (currency).
- HTTP operations: `ListProjectContracts`, `GetContract`, `CreateContract`, `UpdateContract`, `ListContractorContracts`, as in spec §4.

- [ ] **Step 1: Domain**

- `ContractForm { NZS3910, NZS3916, NZS3917, NZIASCC, Consultancy, Other }`
- `ContractStatus { Draft, Active, PracticalCompletion, Complete, Terminated }`
- `ContractFields(string Title, string? Reference, ContractForm Form, Money? Value, DateOnly? StartDate, DateOnly? EndDate, decimal? RetentionPercent, ContractStatus Status, string? Notes)`, using `PDS.Shared.Money`.

`ContractRules`:
- Constants: `TitleMaxLength = 200`, `ReferenceMaxLength = 50`, `NotesMaxLength = 4000`, `MaxRetentionPercent = 20m`.
- `IsOrdered(start, end)`.
- `Normalise`: trim Title; Clean Reference and Notes.
- `EnsureValid`:
  - title required
  - value ≥ 0
  - retention between 0 and 20 with at most 1 decimal place (`decimal.Round(r, 1) == r`)
  - dates in order

`Contract`:
- Properties: `Id` (ContractId), `ProjectId`, `ContractorId`, `Fields`, `Created`, `Updated`, `Version`.
- `static Create(ProjectId, ContractorId, ContractFields, AuditStamp)`.
- `Rehydrate`.
- `Update(ContractFields, AuditStamp)`. The contractor is fixed at creation and has no setter.

- [ ] **Step 2: Data**

`ContractItemMapper`:
- `Key(ProjectId, ContractId)` → pk `PROJECT#{pid}`, sk `CONTRACT#{id}`.
- `ToItem` also sets `gsi1pk = CONTRACTOR#{contractorId}` and `gsi1sk = CONTRACT#{id}`.
- Attributes: `id`, `projectId`, `contractorId`, `title`, `reference?`, `form`, `valueAmount?` + `currency?`, `startDate?`, `endDate?`, `retentionPercent?`, `status`, `notes?`, audit attributes, `version`.
- `FromItem` is the inverse.

`ContractStore`:
- `GetAsync(ProjectId, ContractId)`.
- `ListForProjectAsync(ProjectId)`: Query `pk = :pk AND begins_with(sk, :prefix)` with prefix `CONTRACT#`.
- `ListForContractorAsync(ContractorId)`: Query gsi1 `gsi1pk = :pk` with `CONTRACTOR#{id}`.
- `CreateAsync` (attribute_not_exists) and `UpdateAsync` (version condition → bool).
- Mirror `RiskStore`.

- [ ] **Step 3: Features**

`ContractDtos`:
- `IContractInput`:
  - `Title`, `Reference`
  - `ContractForm? Form`
  - `decimal? ValueAmount`
  - `StartDate`, `EndDate`
  - `decimal? RetentionPercent`
  - `ContractStatus? Status`
  - `Notes`
- `CreateContractRequest(Guid? ContractorId, …)` and `UpdateContractRequest(Guid? ContractorId, …, long Version)`.
- `ToFields(string currency)`: Money from `ValueAmount`.
- `ContractListItem` fields:
  - `Guid Id`, `Guid ProjectId`, `string? ProjectCode`, `string? ProjectName`
  - `Guid ContractorId`, `string ContractorName`
  - `string Title`, `string? Reference`, `ContractForm Form`
  - `decimal? ValueAmount`, `string Currency`
  - `DateOnly? StartDate`, `DateOnly? EndDate`
  - `ContractStatus Status`, `long Version`
- `ContractDetails`: all fields plus `ContractorName`, the audit fields and `Version`.

`ContractValidators` (mirror the project validators):
- ContractorId NotNull ("Choose a contractor.").
- Title required, ≤ 200. Reference ≤ 50.
- Form and Status: NotNull and IsInEnum.
- ValueAmount ≥ 0 with `PrecisionScale(18, 2, true)`.
- RetentionPercent between 0 and 20 with `PrecisionScale(4, 1, true)`.
- EndDate ≥ StartDate when both are set.
- Notes ≤ 4000.
- Update: Version > 0.

`ProjectLookup`: the same as in Risks (`FindAsync(IPortfolioQueries, Guid, ct)`), redeclared in this module because modules don't share internals.

Contracts group: `/api/contractors/projects/{projectId:guid}/contracts` (WithTags "Contracts", CanRead).

- **`ListProjectContracts`**:
  - 404 if the project is unknown.
  - Load the contracts, then batch-get their contractors (`ContractorStore.GetManyAsync` on the distinct ids) for names. A missing contractor shows as "(unknown contractor)".
  - Order: status Active, PracticalCompletion, Draft, Complete, Terminated (explicit rank map), then StartDate, then Title.
  - ProjectCode and ProjectName come from the summary.
- **`GetContract`**: returns `ContractDetails` with the contractor name, or 404.
- **`CreateContract`** (CanWrite, validated):
  - Project unknown → 404; archived → `ProjectArchived()`.
  - Contractor missing or inactive → `ContractorUnavailable()`.
  - Create, then return 201 at `/api/contractors/projects/{projectId}/contracts/{id}`.
- **`UpdateContract`** (CanWrite, validated):
  - Load → 404 if missing.
  - `request.ContractorId != contract.ContractorId` → `ContractorChanged()`.
  - Version mismatch → 409 via `VersionConflict("contract")`.
  - Update, then conditional write → 409.
- **`ListContractorContracts`**, mapped in the contractors group as `GET "/{contractorId:guid}/contracts"`:
  - 404 if the contractor is unknown.
  - Load its contracts via gsi1.
  - Fetch the project summaries with `IPortfolioQueries.GetSummaries(distinct project ids)` for code and name.
  - Order by StartDate descending (nulls last), then Title.

`ContractorsModule`: register `ContractStore` as a singleton and map the contracts group.

- [ ] **Step 4: Build, manual check, commit**

- Run the build and `gen:api` as in Task 1.
- **Manual check:**
  - Create a project and a contractor, then POST a contract (value 150000.50, retention 5): 201.
  - List the project's contracts: it shows the contractor name.
  - `GET /api/contractors/{id}/contracts` shows the project code.
  - POST with an inactive contractor → 400 on `contractorId`.
  - POST with an unknown contractor → 400 on `contractorId`.
  - PUT with a different contractorId → 400 on `contractorId`.
  - PUT with a stale version → 409.
  - Retention 25 → 400 on `retentionPercent`.
  - End before start → 400 on `endDate`.
  - Archive the project, then POST → 400 on `projectId`.
  - Unknown project list → 404.
- **Commit:** `feat(contractors): per-project contracts API`.

---

### Task 3: Contractor pages (web)

**Files:**
- Create in `web/src/features/contractors/`: `types.ts`, `api.ts`, `ComplianceBadge.tsx`, `contractorFormModel.ts`, `ContractorForm.tsx`, `ContractorsPage.tsx`, `ContractorDetailPage.tsx`, `ContractorCreatePage.tsx`, `ContractorEditPage.tsx`
- Modify: `web/src/routes.tsx` (routes `contractors`, `contractors/new`, `contractors/:id`, `contractors/:id/edit`), `web/src/shared/layout/AppLayout.tsx` (a "Contractors" NavLink after "Projects")

**Interfaces:**
- Consumes the Task 1 and 2 API through the generated schema.
- Produces:
  - `types.ts` exports:
    - the types `ContractorListItem`, `ContractorDetails`, `ContractorInput`, `ContractorUpdateInput`, `ContractorContract`
    - the unions `contractorTypes`, `ContractorType`, `ComplianceStatus`, `contractForms`, `ContractForm`, `contractStatuses`, `ContractStatus`
    - the label maps `contractorTypeLabel`, `complianceLabel`, `complianceColor`, `contractFormLabel`, `contractStatusLabel`, `contractStatusColor`
  - `api.ts` exports the hooks `useContractors({ search, type, includeInactive })`, `useContractor(id)`, `useContractorContracts(id)`, `useCreateContractor()`, `useUpdateContractor(id)` and `useSetContractorActive(id)`.
  - `<ComplianceBadge status />`.

Label maps:

| Map | Values |
|---|---|
| `complianceColor` | Current green, ExpiringSoon yellow, Expired red, NotRecorded gray |
| `complianceLabel` | Current, Expiring soon, Expired, Not recorded |
| `contractFormLabel` | NZS3910 "NZS 3910", NZS3916 "NZS 3916", NZS3917 "NZS 3917", NZIASCC "NZIA SCC", Consultancy "Consultancy (CCCS)", Other "Other" |
| `contractStatusLabel` | Draft, Active, "Practical completion", Complete, Terminated |
| `contractStatusColor` | Draft gray, Active blue, PracticalCompletion teal, Complete green, Terminated red |

Types are derived from the generated `paths`, as in `features/risks/types.ts`. The literal unions must equal the generated enums; no casts.

- [ ] **Step 1: Types, hooks and the badge.**
  - Mirror `features/portfolio/api.ts` and `features/risks/api.ts`.
  - Query keys: `['contractors', query]`, `['contractor', id]`, `['contractor-contracts', id]`.
  - Mutations `setQueryData(['contractor', id])` and invalidate `['contractors']`.
- [ ] **Step 2: Form model and form.**
  - `contractorFormModel.ts` mirrors `projectFormModel.ts`: values are all strings, with `type: ContractorType | null`.
  - Zod rules mirror the server:
    - name required, ≤ 200
    - email valid when present (`z.email()` or `.refine`)
    - NZBN `^\d{13}$` when present
    - lengths as in the spec
    - dates `yyyy-MM-dd` or empty
  - `toContractorInput` maps empty strings to null. `fromContractor` is the inverse.
  - `ContractorForm.tsx` mirrors `ProjectForm.tsx`: Fieldsets "Contractor" (Name, Type, Trade, NZBN), "Contact" (Contact name, Email, Phone, Address), "Compliance" (Public liability expiry, Professional indemnity expiry, Licence number, Licence expiry, all dates `type="date"`) and "Notes". Server 400s map onto the fields, other errors go to `onError`.
- [ ] **Step 3: Pages.**
  - **`ContractorsPage`** mirrors `ProjectsPage` without paging: search (debounced 300 ms), type Select, "Show inactive" Switch, a "New contractor" button for canWrite, and a table (Name link, Type, Trade, ComplianceBadge, Status). Empty state: "No contractors match these filters."
  - **`ContractorDetailPage`** mirrors `ProjectDetailPage`:
    - Header: name, Type badge, ComplianceBadge, and an Inactive badge when inactive.
    - Actions: Edit (canWrite); Deactivate or Activate (canAdminister) with the version, and a 409 notification.
    - Cards: Contact; Compliance (each expiry with its date and badge, plus the licence number); Notes.
    - Contracts card: a table of project (link `/projects/{projectId}` showing `projectCode · projectName`), title, form label, value (`formatMoney`), start–end (`formatDate`) and a status badge, with the empty state "No contracts yet."
    - 404 uses `NotFoundPage`.
  - **`ContractorCreatePage`** and **`ContractorEditPage`** mirror the project create and edit pages, including the role guard and the 409 conflict alert with "Reload latest" on edit.
  - Add the routes and the NavLink.
- [ ] **Step 4: Verify and commit.** Run typecheck, lint and build. Then, through the Vite proxy, curl `/contractors` and `/contractors/<id>` to confirm the SPA is served. Commit `feat(web): contractor register pages`.

---

### Task 4: Contracts section on the project detail page (web)

**Files:**
- Create in `web/src/features/contractors/`: `contractFormModel.ts`, `ContractFormModal.tsx`, `ProjectContractsSection.tsx`
- Modify: `web/src/features/contractors/api.ts` (add `useProjectContracts(projectId)`, `useCreateContract(projectId)` and `useUpdateContract(projectId)`), `web/src/features/portfolio/ProjectDetailPage.tsx` (render the section after the Risks section)

- [ ] **Step 1: Hooks.**
  - `useProjectContracts` uses key `['project-contracts', projectId]`.
  - The mutations invalidate that key and `['contractor-contracts']` in `onSettled`.
- [ ] **Step 2: Form model and modal.**
  - `contractFormModel.ts`:
    - Values: contractorId (string | null), title, reference, form (ContractForm | null), valueAmount (number | ''), startDate, endDate, retentionPercent (number | ''), status (ContractStatus, default 'Draft'), notes.
    - Zod mirrors the server: contractor required, title required, value ≥ 0 with ≤ 2 dp, retention 0–20 with ≤ 1 dp, end ≥ start on `endDate`.
    - `toContractInput` and `fromContract`.
  - `ContractFormModal.tsx` mirrors `RiskFormModal`, with a keyed inner component.
    - **Contractor Select:** searchable, with data from `useContractors({ includeInactive: false })` (label `name — trade`), disabled when editing.
    - The selected contractor's `ComplianceBadge` shows beside it, with a "(check insurance)" hint when it's not Current.
    - **Other fields:** Title, Reference, Form Select (labels), Value NumberInput (use `numberInputParts` like `ProjectForm`), Start and End dates, Retention % NumberInput (decimalScale 1, max 20), Status Select (all five), Notes.
    - **Errors:** reuse the error-handling approach of `features/risks/riskMutationError.ts`. Copy it into the contractors feature as a small generic helper, or move it to `web/src/shared/mutationError.ts` and import it from both features. Prefer moving it to shared and updating the risks imports. Unmapped keys (e.g. `projectId`, `contractorId` when disabled) go to the top alert.
- [ ] **Step 3: Section.**
  - `ProjectContractsSection({ projectId, projectArchived })` mirrors `ProjectRisksSection`: a card titled "Contracts" with "Add contract" (canWrite and not archived).
  - Table columns: Contractor (link to `/contractors/{contractorId}`), Title, Reference, Form, Value, Start–End, Status badge, and Edit for canWrite.
  - Empty state: "No contracts recorded."
  - Render it in `ProjectDetailPage` after `ProjectRisksSection`.
- [ ] **Step 4: Verify and commit.** Run typecheck, lint and build, then the manual proxy check (create a contract via curl and list it through the proxy). Commit `feat(web): project contracts section`.

## Spec coverage

| Spec | Task |
|---|---|
| §2 module, contracts-only reference, wiring | 1 |
| §3 contractor data and access | 1 |
| §3 contract data and access | 2 |
| §4 contractor API | 1 (and the contractor-contracts route in 2) |
| §4 contract API | 2 |
| §5 contractor pages and nav | 3 |
| §5 project contracts section | 4 |
| Review Focus 1–5 | 2 (1, 2, 4), 1 (3, 5), 3–4 (UI 409 handling) |
