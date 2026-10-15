using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Cashflow.Domain;
using PDS.Costs.Contracts;
using PDS.Portfolio.Contracts;
using PDS.Sales.Contracts;
using PDS.Shared.Settings;

namespace PDS.Cashflow.Features;

internal static class GetProjectCashflow
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("", Handle).WithName("GetProjectCashflow");

    internal static async Task<Results<Ok<ProjectCashflow>, NotFound>> Handle(
        Guid projectId, IPortfolioQueries portfolio, ICostQueries costs, ISalesQueries sales,
        TimeProvider clock, IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var pid = new ProjectId(projectId);
        var project = (await portfolio.GetSummaries([pid], ct)).FirstOrDefault();
        if (project is null)
            return TypedResults.NotFound();

        var schedule = await portfolio.GetSchedule(pid, ct) ?? new ProjectSchedule(null, null, null, null);
        var position = await costs.GetProjectCostPosition(pid, ct);
        var contracted = await sales.GetContractedSales(pid, ct);
        var today = LocalDate.Today(clock, locale.Value);
        var result = CashflowCalculator.Calculate(today, schedule, position, contracted);

        var months = result.Months
            .Select(m => new CashflowMonthDto(
                Month.Key(m.Month), m.CostActual, m.CostForecast, m.Outflow,
                m.SalesSettled, m.SalesForecast, m.Inflow, m.Net, m.Cumulative))
            .ToList();
        var totals = new CashflowTotalsDto(
            result.Months.Sum(m => m.CostActual), result.Months.Sum(m => m.CostForecast), result.Months.Sum(m => m.Outflow),
            result.Months.Sum(m => m.SalesSettled), result.Months.Sum(m => m.SalesForecast), result.Months.Sum(m => m.Inflow),
            result.Months.Sum(m => m.Net),
            result.UnscheduledCost, result.UnscheduledSales.Sum(u => u.SalePrice),
            result.PeakFunding, result.PeakFundingMonth is { } pm ? Month.Key(pm) : null);
        var unscheduled = result.UnscheduledSales
            .Select(u => new UnscheduledSaleDto(u.UnitId.Value, u.UnitIdentifier, u.Status.ToString(), u.SalePrice))
            .ToList();

        return TypedResults.Ok(new ProjectCashflow(
            projectId, project.Code, project.Name, project.IsArchived, locale.Value.Currency, today,
            result.ScheduleStart, result.ScheduleCompletion, months, totals, unscheduled));
    }
}
