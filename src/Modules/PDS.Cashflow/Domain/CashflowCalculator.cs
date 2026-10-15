using PDS.Costs.Contracts;
using PDS.Portfolio.Contracts;
using PDS.Sales.Contracts;

namespace PDS.Cashflow.Domain;

internal static class CashflowCalculator
{
    public static CashflowResult Calculate(
        DateOnly today, ProjectSchedule schedule, ProjectCostPosition cost, IReadOnlyList<ContractedSale> sales)
    {
        var current = Month.Of(today);
        var start = schedule.ActualStart ?? schedule.PlannedStart;
        var completion = schedule.ActualCompletion ?? schedule.PlannedCompletion;

        var costActual = new Dictionary<DateOnly, decimal>();
        foreach (var a in cost.Actuals)
            Add(costActual, Month.Of(a.Date), a.Amount);

        IReadOnlyDictionary<DateOnly, decimal> costForecast = new Dictionary<DateOnly, decimal>();
        var unscheduledCost = 0m;
        if (completion is null)
            unscheduledCost = cost.ForecastToComplete;
        else
        {
            var from = Month.Max(current, Month.Of(start ?? today));
            costForecast = CostPhasing.StraightLine(cost.ForecastToComplete, from, Month.Of(completion.Value));
        }

        var salesSettled = new Dictionary<DateOnly, decimal>();
        var salesForecast = new Dictionary<DateOnly, decimal>();
        var unscheduledSales = new List<ContractedSale>();
        foreach (var s in sales)
        {
            if (s.SettlementDate is not { } date)
                unscheduledSales.Add(s);
            else if (s.Status == ContractedSaleStatus.Settled)
                Add(salesSettled, Month.Of(date), s.SalePrice);
            else
                Add(salesForecast, Month.Max(Month.Of(date), current), s.SalePrice);
        }

        var touched = costActual.Keys.Concat(costForecast.Keys).Concat(salesSettled.Keys).Concat(salesForecast.Keys)
            .Append(current).ToList();
        if (start is { } s0) touched.Add(Month.Of(s0));
        if (completion is { } c0) touched.Add(Month.Of(c0));
        var first = touched.Min();
        var last = touched.Max();

        var months = new List<CashflowMonth>();
        var cumulative = 0m;
        decimal? peak = null;
        DateOnly? peakMonth = null;
        foreach (var m in Month.Range(first, last))
        {
            var row = new CashflowMonth(m, costActual.GetValueOrDefault(m), costForecast.GetValueOrDefault(m),
                salesSettled.GetValueOrDefault(m), salesForecast.GetValueOrDefault(m), 0m);
            cumulative += row.Net;
            months.Add(row with { Cumulative = cumulative });
            if (cumulative < 0m && (peak is null || -cumulative > peak))
            {
                peak = -cumulative;
                peakMonth = m;
            }
        }

        return new CashflowResult(start, completion, months, unscheduledCost,
            unscheduledSales.OrderBy(u => u.UnitIdentifier, StringComparer.OrdinalIgnoreCase).ToList(), peak, peakMonth);
    }

    private static void Add(Dictionary<DateOnly, decimal> map, DateOnly m, decimal amount) =>
        map[m] = map.GetValueOrDefault(m) + amount;
}
