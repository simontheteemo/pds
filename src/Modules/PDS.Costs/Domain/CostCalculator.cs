using PDS.Costs.Contracts;

namespace PDS.Costs.Domain;

internal static class CostCalculator
{
    public static CostFigures ForLine(decimal budget, decimal? forecastRemaining, decimal committed, decimal actual)
    {
        var exposure = Math.Max(committed, actual);
        var remaining = forecastRemaining ?? Math.Max(budget - exposure, 0m);
        var ffc = exposure + remaining;
        return new CostFigures(budget, committed, actual, ffc, ffc - actual, budget - ffc);
    }

    /// <summary>Per-line figures keyed by line id, and the project total (the sum of the line rows).</summary>
    public static (IReadOnlyDictionary<BudgetLineId, CostFigures> Lines, CostFigures Total) For(
        IReadOnlyList<BudgetLine> lines, IReadOnlyList<Commitment> commitments, IReadOnlyList<ActualCost> actuals)
    {
        var committed = commitments.GroupBy(c => c.Fields.BudgetLineId).ToDictionary(g => g.Key, g => g.Sum(c => c.Fields.Amount.Amount));
        var actual = actuals.GroupBy(a => a.Fields.BudgetLineId).ToDictionary(g => g.Key, g => g.Sum(a => a.Fields.Amount.Amount));
        var perLine = lines.ToDictionary(
            l => l.Id,
            l => ForLine(l.Fields.Budget.Amount, l.Fields.ForecastRemaining?.Amount,
                         committed.GetValueOrDefault(l.Id), actual.GetValueOrDefault(l.Id)));
        var total = perLine.Values.Aggregate(CostFigures.Zero, (sum, f) => sum.Plus(f));
        return (perLine, total);
    }
}
