using PDS.Sales.Contracts;

namespace PDS.Cashflow.Domain;

internal sealed record CashflowResult(
    DateOnly? ScheduleStart, DateOnly? ScheduleCompletion,
    IReadOnlyList<CashflowMonth> Months,
    decimal UnscheduledCost,
    IReadOnlyList<ContractedSale> UnscheduledSales,
    decimal? PeakFunding, DateOnly? PeakFundingMonth);
