namespace PDS.Costs.Contracts;

/// <summary>Project cost position, consistent with the cost report totals.</summary>
public sealed record ProjectCostPosition(
    decimal ForecastFinalCost, decimal Actual, decimal ForecastToComplete,
    IReadOnlyList<CostActualEntry> Actuals);
