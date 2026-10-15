namespace PDS.Cashflow.Features;

internal sealed record CashflowMonthDto(
    string Month, decimal CostActual, decimal CostForecast, decimal Outflow,
    decimal SalesSettled, decimal SalesForecast, decimal Inflow, decimal Net, decimal Cumulative);

internal sealed record CashflowTotalsDto(
    decimal CostActual, decimal CostForecast, decimal Outflow,
    decimal SalesSettled, decimal SalesForecast, decimal Inflow, decimal Net,
    decimal UnscheduledCost, decimal UnscheduledSales,
    decimal? PeakFunding, string? PeakFundingMonth);

internal sealed record UnscheduledSaleDto(Guid UnitId, string UnitIdentifier, string Status, decimal SalePrice);

internal sealed record ProjectCashflow(
    Guid ProjectId, string ProjectCode, string ProjectName, bool ProjectArchived,
    string Currency, DateOnly AsOf, DateOnly? ScheduleStart, DateOnly? ScheduleCompletion,
    List<CashflowMonthDto> Months, CashflowTotalsDto Totals, List<UnscheduledSaleDto> UnscheduledSales);
