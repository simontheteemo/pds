using PDS.Costs.Data;
using PDS.Costs.Domain;
using PDS.Portfolio.Contracts;
using PDS.Reporting.Contracts;

namespace PDS.Costs.Features;

internal sealed class CostLinesExport(CostStore store, IPortfolioQueries portfolio) : IExportDataset
{
    public string Name => "cost-lines";

    public string Title => "Cost report lines";

    public string Description => "Every budget line in every project with budget, committed, actual, forecast final cost and variance (excl. GST).";

    public async Task<ExportTable> LoadAsync(CancellationToken ct)
    {
        var all = await store.ScanAllAsync(ct);
        var projects = (await portfolio.GetSummaries(all.Keys.ToList(), ct)).ToDictionary(p => p.Id);
        var rows = new List<Row>();
        foreach (var (projectId, data) in all)
        {
            var (figures, _) = CostCalculator.For(data.Lines, data.Commitments, data.Actuals);
            var project = projects.GetValueOrDefault(projectId);
            rows.AddRange(data.Lines.Select(l => new Row(l, project, figures[l.Id])));
        }

        var ordered = rows
            .OrderBy(x => x.Project?.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Line.Fields.SortOrder)
            .ThenBy(x => x.Line.Fields.Name, StringComparer.OrdinalIgnoreCase);

        return ExportTable.From<Row>(ordered,
        [
            new("budget_line_id", x => x.Line.Id.Value),
            new("project_id", x => x.Line.ProjectId.Value),
            new("project_code", x => x.Project?.Code),
            new("project_name", x => x.Project?.Name),
            new("line_name", x => x.Line.Fields.Name),
            new("sort_order", x => x.Line.Fields.SortOrder),
            new("budget", x => x.Figures.Budget),
            new("committed", x => x.Figures.Committed),
            new("actual", x => x.Figures.Actual),
            new("forecast_remaining", x => x.Line.Fields.ForecastRemaining?.Amount),
            new("forecast_final_cost", x => x.Figures.ForecastFinalCost),
            new("forecast_to_complete", x => x.Figures.ForecastToComplete),
            new("variance", x => x.Figures.Variance),
            new("currency", x => x.Line.Fields.Budget.Currency),
            new("notes", x => x.Line.Fields.Notes),
        ]);
    }

    private sealed record Row(BudgetLine Line, ProjectSummary? Project, CostFigures Figures);
}
