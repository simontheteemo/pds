namespace PDS.Costs.Domain;

internal sealed record CostFigures(
    decimal Budget, decimal Committed, decimal Actual,
    decimal ForecastFinalCost, decimal ForecastToComplete, decimal Variance)
{
    public static readonly CostFigures Zero = new(0, 0, 0, 0, 0, 0);

    public CostFigures Plus(CostFigures o) => new(
        Budget + o.Budget, Committed + o.Committed, Actual + o.Actual,
        ForecastFinalCost + o.ForecastFinalCost, ForecastToComplete + o.ForecastToComplete, Variance + o.Variance);
}
