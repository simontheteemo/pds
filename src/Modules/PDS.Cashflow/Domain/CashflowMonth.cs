namespace PDS.Cashflow.Domain;

internal sealed record CashflowMonth(
    DateOnly Month, decimal CostActual, decimal CostForecast, decimal SalesSettled, decimal SalesForecast, decimal Cumulative)
{
    public decimal Outflow => CostActual + CostForecast;
    public decimal Inflow => SalesSettled + SalesForecast;
    public decimal Net => Inflow - Outflow;
}
