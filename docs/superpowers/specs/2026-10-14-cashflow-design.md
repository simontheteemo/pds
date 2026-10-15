# PDS Cashflow (v1) — Design Spec

- Date: 2026-10-15
- Author: @simon (with Claude)
- Status: **Proceeding autonomously under the parallel-build brief.** Every decision below was made by Claude and is open to review. Implementation is **deferred until Cost Tracking (PR #4) merges to main**.
- Builds on: [Foundation spec](2026-09-29-foundation-design.md) (roadmap item 6, "Documents, reporting export, cashflow") · [Costs spec](2026-10-14-costs-design.md) · [Sales spec](2026-10-14-sales-design.md) · architecture page: "Cashflow is calculated from costs and sales, not stored."

## 1. Purpose

Give each project a **monthly cashflow forecast**: money out (costs) and money in (sales) per month, the net per month, and the cumulative position. The headline figure is **peak funding**: the most negative cumulative position, and the month it happens. That is the number a developer takes to the bank.

### Stated vs decided

| Stated (architecture / coordinator brief) | Decided by Claude (review later) |
|---|---|
| "Cashflow is calculated from costs and sales, not stored" | **The new module `PDS.Cashflow` owns no table.** It computes on read from three contract queries: Portfolio, Costs and Sales. There's no infra change |
| Outflows: actuals by date plus remaining forecast cost spread over time | Actual costs go in the month of their `Date` (the invoice or claim date). The spec has no payment date, so invoice date stands in for cash date. **Remaining cost** = the project's `ForecastToComplete` from the cost report (Σ per line, always ≥ 0) |
| Spread method: straight-line over project dates, or per-line dates added to Costs | **Straight-line over the project's remaining schedule.** It runs from `max(current month, start month)` to the completion month, inclusive. `start = ActualStart ?? PlannedStart`; `completion = ActualCompletion ?? PlannedCompletion`. Per-line spend profiles aren't added in v1: they would need a Costs schema change and a UI to maintain them, and nobody has asked for them. The spread lives in one function (`CostPhasing`), so a per-line or S-curve profile can replace it later |
| — | No completion date → the remaining cost isn't phased. It's reported as `unscheduledCost`, and the page says to set the project's planned completion. A completion month before the current month (overdue) → all remaining cost goes in the current month |
| — | Each month's share is rounded to 2 dp. The last month takes the remainder, so the phased months sum exactly to `ForecastToComplete` |
| Inflows: settled amounts by settlement date; contracted unsettled sales by expected settlement date | **Settled** units: the sale price in the settlement month. **Conditional and Unconditional** units: the sale price in the month of their settlement date (the due date). If that month is already past, the sale hasn't settled yet, so it moves to the current month. If there's no settlement date, the sale is reported as unscheduled and listed by unit |
| — | Conditional sales count as forecast inflow, the same as Unconditional ones. The per-unit list shows each status, and the risk is noted (§7). Reserved, Available and Withdrawn units contribute nothing: unsold stock isn't forecast in v1 |
| Deposits optional; decide | **Deposits are excluded.** In NZ, residential deposits are normally held in the agent's or solicitor's trust account until settlement, so the developer receives the full price at settlement. Treating the full sale price as a settlement inflow avoids double counting |
| Charts: cumulative and net position per month | One composite chart: monthly inflow bars (positive), monthly outflow bars (shown negative), and a cumulative net position line. A month-by-month table sits below it |
| Chart library: check package.json; justify any addition | Nothing exists. **Add `@mantine/charts@9.6.3`** (Mantine's own package, version-locked to `@mantine/core` 9.6.3, and themed and dark-mode aware like the rest of the UI) plus its peer **`recharts`** (pinned exact, ≥ 3.2.1). The cashflow page is **lazy-loaded** via the route's `lazy`, so recharts stays out of the main bundle. A hand-rolled SVG chart was rejected: about 200 lines of bespoke axis, tooltip and theming code to maintain |
| New stored data: minimal, and decide the owner | **None.** Each upstream module gets one additive read-only contract query (§2.1) |
| Portfolio view optional; decide | **Not built in v1.** Costs has no all-projects query, so a portfolio view would cost N report loads, and nobody has asked for it. The calculator takes per-project inputs, so a later roll-up can sum projects |
| — | Range of months: from the earliest of (first actual, first sale month, schedule start, current month) to the latest of (last actual, last sale month, completion, current month). Months with no movement inside the range are included as zero rows, so the chart has a continuous axis |
| — | "Current month" is the month of `LocalDate.Today` in `Locale:TimeZone`, the shared helper on main |
| — | GST: costs exclude GST (the Costs spec). The Sales spec doesn't say, so sale prices are used as entered. The page notes "Costs exclude GST; sale prices as entered in Sales." No GST adjustment is made |
| — | Read-only: CanRead only. Archived projects still show their cashflow |
| No new tests (standing directive) | Verify by format, build, typecheck, lint and synth, plus manual API checks against DynamoDB Local with seeded costs and sales |

### Success criteria
- `GET /api/cashflow/projects/{projectId}` returns the monthly series, the totals, peak funding, and the unscheduled amounts. Its figures reconcile:
  - Σ cost outflows + unscheduled cost = cost report `Actual` + `ForecastToComplete`
  - Σ inflows + unscheduled sales = Sales `ContractedValue`
- The project detail page shows a **Cashflow** card: peak funding, its month, and a link.
- The **Cashflow page** (`/projects/:id/cashflow`) shows:
  - a figures strip
  - the composite chart
  - the monthly table
  - warnings for an unscheduled cost or unscheduled sales
- `PDS.Cashflow` references only `PDS.Portfolio.Contracts`, `PDS.Costs.Contracts` and `PDS.Sales.Contracts`.

## 2. Architecture

- A new module, `src/Modules/PDS.Cashflow`:
  - `CashflowModule` (public): `AddCashflowModule()`, `MapCashflowEndpoints()`. It has no `Tables` and no infra line.
  - `Domain/` (internal): `CashflowCalculator`, `CostPhasing`, `MonthKey` helpers.
  - `Features/` (internal): `GetProjectCashflow`, `CashflowDtos`.
- Project references: PDS.Shared, PDS.Portfolio, PDS.Costs, PDS.Sales (Contracts only, by the module rule).
- Host: one line each, `AddCashflowModule()` and `MapCashflowEndpoints()`, plus one ProjectReference and one slnx line.
- Web: `web/src/features/cashflow`, one lazy route `projects/:id/cashflow`, and one `<ProjectCashflowCard>` line on `ProjectDetailPage`. There's no nav item.

### 2.1 Required contract queries (additive; Task 1)

**Portfolio** (`PDS.Portfolio/Contracts`). `ProjectSummary` has no dates, so add:

```csharp
// Contracts/ProjectSchedule.cs
public sealed record ProjectSchedule(
    DateOnly? PlannedStart, DateOnly? PlannedCompletion, DateOnly? ActualStart, DateOnly? ActualCompletion);

// IPortfolioQueries — new member
/// <summary>The project's dates, or null when the project does not exist.</summary>
Task<ProjectSchedule?> GetSchedule(ProjectId id, CancellationToken ct);
```

`PortfolioQueries` implements it with `ProjectStore.GetAsync`. No test fakes implement `IPortfolioQueries` today, so the new member breaks nothing.

**Costs** (`PDS.Costs/Contracts`):

```csharp
public sealed record CostActualEntry(DateOnly Date, decimal Amount);

/// <summary>Project cost position, consistent with the cost report totals.</summary>
public sealed record ProjectCostPosition(
    decimal ForecastFinalCost, decimal Actual, decimal ForecastToComplete,
    IReadOnlyList<CostActualEntry> Actuals);

public interface ICostQueries
{
    /// <summary>Zeros and no actuals for a project with no cost data (or an unknown project).</summary>
    Task<ProjectCostPosition> GetProjectCostPosition(ProjectId projectId, CancellationToken ct);
}
```

- `Data/CostQueries.cs` implements it with `CostStore.LoadProjectAsync` and `CostCalculator.For`, and is registered as a singleton.
- `Actuals` holds only actuals whose budget line exists. That matches the report: an orphan actual (from a line-delete race) counts toward no line. So `Σ Actuals = Actual`.

**Sales** (`PDS.Sales/Contracts`):

```csharp
public enum ContractedSaleStatus { Conditional, Unconditional, Settled }

public sealed record ContractedSale(
    UnitId UnitId, string UnitIdentifier, ContractedSaleStatus Status,
    decimal SalePrice, DateOnly? SettlementDate);

public interface ISalesQueries
{
    /// <summary>Units with a contracted sale (Conditional, Unconditional, Settled). Empty for an unknown project.</summary>
    Task<IReadOnlyList<ContractedSale>> GetContractedSales(ProjectId projectId, CancellationToken ct);
}
```

- `Data/SalesQueries.cs` implements it with `UnitStore.ListForProjectAsync`, filtered by `SaleRules.IsContracted`. A missing price (which the rules forbid) maps to 0.
- It's registered as a singleton.
- `Σ SalePrice` equals `SalesTotals.ContractedValue`.

## 3. Calculation (`CashflowCalculator`, pure)

Inputs: `today`, `ProjectSchedule`, `ProjectCostPosition`, `ContractedSale[]`. Months are represented by the first day of the month (`DateOnly`) internally and serialised as `"yyyy-MM"`.

```
current       = month(today)
start         = schedule.ActualStart ?? schedule.PlannedStart
completion    = schedule.ActualCompletion ?? schedule.PlannedCompletion

costActual[m]    = Σ actual.Amount where month(actual.Date) = m
remaining        = cost.ForecastToComplete
if completion is null:          unscheduledCost = remaining
else:
    from = max(current, month(start ?? today))
    to   = max(month(completion), from)          # overdue or a start after completion → a single month
    n    = months(from..to inclusive)
    share = round(remaining / n, 2, AwayFromZero); the last month gets remaining − share·(n−1)
    costForecast[m] = share (or the remainder) for m in from..to

for sale in sales:
    Settled                     → salesSettled[month(SettlementDate)] += SalePrice
                                   (SettlementDate is required at Settled; if missing, it's treated as unscheduled)
    Conditional/Unconditional   → SettlementDate null → unscheduledSales += SalePrice, listed
                                   else salesForecast[max(month(SettlementDate), current)] += SalePrice

months = continuous range [min(all touched months, start month, current) .. max(all touched months, completion month, current)]
outflow = costActual + costForecast
inflow  = salesSettled + salesForecast
net     = inflow − outflow
cumulative = running Σ net
peakFunding = min(0, min cumulative) (reported as a positive amount) with its first month; null when the cumulative never goes negative
```

- Only months that hold data, the start or completion month, or the current month can extend the range, so an empty project returns exactly one month (the current month), all zeros.
- **Range guard:** a range longer than 600 months (dates mistyped decades out) still computes. The API returns it as is; the UI only renders. This is noted as a risk rather than validated, because the upstream modules own those dates.

Totals: `costActual`, `costForecast`, `outflow`, `salesSettled`, `salesForecast`, `inflow`, `net`, `unscheduledCost`, `unscheduledSales`, `peakFunding`, `peakFundingMonth`.

## 4. API

`GET /api/cashflow/projects/{projectId:guid}` (tag "Cashflow", CanRead, name `GetProjectCashflow`):
- It returns 404 for an unknown project (from `IPortfolioQueries.GetSummaries`).
- `GetSchedule` is called as well. A null schedule after a found summary (a race) is treated as all-null dates.
- The response, `ProjectCashflow`:
  - project: `projectId`, `projectCode`, `projectName`, `projectArchived`
  - `currency`, `asOf` (today)
  - `scheduleStart`, `scheduleCompletion`: the dates used, nullable
  - `months[]`: `{ month: "yyyy-MM", costActual, costForecast, outflow, salesSettled, salesForecast, inflow, net, cumulative }`
  - `totals`: as in §3
  - `unscheduledSales[]`: `{ unitId, unitIdentifier, status, salePrice }`, ordered by identifier

## 5. UI

- **`ProjectCashflowCard`** (project detail page, after the Costs card):
  - Title "Cashflow".
  - Peak funding, shown as "Peak funding $X in Mon YYYY", or "No funding gap forecast".
  - Net position at the end of the range.
  - An "Open cashflow" link.
- **Cashflow page** (`/projects/:id/cashflow`, lazy-loaded):
  - **Header:** a breadcrumb back to the project (`code · name`), the title "Cashflow", and "As of {asOf}".
  - **Note:** "Costs exclude GST; sale prices as entered in Sales. Remaining cost is spread evenly to the project's completion date."
  - **Figures strip:** Total out, Total in, Net, Peak funding (with its month), Unscheduled cost, Unscheduled sales (each hidden when 0).
  - **Warnings** (yellow Alert):
    - `unscheduledCost > 0` → "Set the project's planned completion date to phase the remaining cost", with a link to the project edit page.
    - `unscheduledSales.length > 0` → "N contracted sales have no settlement date", listing the unit identifiers.
  - **Chart:** a Mantine `CompositeChart`, x = month label (`Oct 2026`):
    - Bars: "In" (teal) and "Out" (red, plotted as a negative value).
    - Line: "Cumulative" (blue).
    - The y-axis uses compact currency ticks (`Intl.NumberFormat` with `notation: "compact"`, a local helper in the chart component), and a zero reference line is drawn.
  - **Table:** Month, Cost (actual), Cost (forecast), Sales (settled), Sales (forecast), Net, Cumulative.
    - The current month row is highlighted.
    - Negative cumulative values are shown in red.
- **Empty state:** if there are no costs, no sales and no schedule, show "Nothing to forecast yet: add budget lines in the cost report or contracted sales", with links.
- Money uses `formatMoney` from `shared/format`.

## 6. Out of scope (v1)
- Per-line spend profiles or planned dates, S-curves, and manual phasing overrides
- Payment dates and retentions, so the cash timing of costs is approximated by the invoice date
- Deposits as inflows, GST cashflows, finance costs and interest, and funding drawdowns
- Forecasting unsold stock (Available or Reserved units at list price)
- A portfolio-wide cashflow roll-up
- Export (CSV or PDF), which belongs with the roadmap item 6 reporting export
- Storing snapshots of past forecasts

## 7. Risks
| # | Risk | Handling |
|---|---|---|
| 1 | Straight-line phasing misrepresents front- or back-loaded spend (land early, construction S-curve) | A documented, simplest-defensible rule in one function (`CostPhasing`). Per-line profiles are the next step if the user wants them |
| 2 | Conditional sales may fall over, so the forecast inflow is optimistic | Statuses are visible per unit, the Sales register shows them, and a later version can split Conditional into its own series |
| 3 | GST basis mismatch between costs (ex GST) and sale prices (unspecified) | Stated on the page. It needs a decision from the client on the Sales GST basis |
| 4 | Branch base: `feat/costs` doesn't contain Sales or the shared `LocalDate` | Implementation starts by rebasing `feat/cashflow` onto main after Costs merges (plan precondition) |
| 5 | Adding a member to `IPortfolioQueries`, and the new queries in Costs and Sales, touch other modules | Each is additive (new files plus one DI line plus one interface member). They're called out in the PR |
| 6 | Bundle size from recharts | The page is lazy-loaded. The plan records the main and lazy chunk sizes from `vite build` |
