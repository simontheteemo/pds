using PDS.Costs.Domain;

namespace PDS.Costs.Features;

internal sealed record CostFiguresDto(
    decimal Budget, decimal Committed, decimal Actual,
    decimal ForecastFinalCost, decimal ForecastToComplete, decimal Variance)
{
    public static CostFiguresDto From(CostFigures f) =>
        new(f.Budget, f.Committed, f.Actual, f.ForecastFinalCost, f.ForecastToComplete, f.Variance);
}

internal sealed record CostReportLine(BudgetLineDetails Line, CostFiguresDto Figures);

internal sealed record CostReportCommitment(
    CommitmentDetails Entry, string BudgetLineName, string? ContractTitle, string? ContractorName);

internal sealed record CostReportActual(
    ActualCostDetails Entry, string BudgetLineName, string? ContractTitle, string? ContractorName);

internal sealed record CostReportContract(
    Guid Id, Guid ContractorId, string ContractorName, string Title, string? Reference, string Status, decimal? ValueAmount);

internal sealed record ProjectCostReport(
    Guid ProjectId, string ProjectCode, string ProjectName, bool ProjectArchived, string Currency,
    CostFiguresDto Totals, List<CostReportLine> Lines, List<CostReportCommitment> Commitments,
    List<CostReportActual> Actuals, List<CostReportContract> Contracts);
