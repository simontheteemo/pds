using Microsoft.Extensions.Options;
using PDS.Portfolio.Data;
using PDS.Portfolio.Domain;
using PDS.Reporting.Contracts;
using PDS.Shared.Settings;

namespace PDS.Portfolio.Features;

internal sealed class MilestonesExport(
    ProjectStore projects, MilestoneStore milestones, TimeProvider clock, IOptions<LocaleOptions> locale) : IExportDataset
{
    private sealed record Row(Project Project, Milestone Milestone, MilestoneEvaluation Evaluation);

    public string Name => "milestones";

    public string Title => "Milestones";

    public string Description => "Every project milestone with planned, forecast and actual dates, status and slippage.";

    public async Task<ExportTable> LoadAsync(CancellationToken ct)
    {
        var today = LocalDate.Today(clock, locale.Value);
        var byProject = (await projects.ListAllAsync(ct)).ToDictionary(p => p.Id);
        var rows = (await milestones.ListAllAsync(ct))
            .Where(m => byProject.ContainsKey(m.ProjectId))
            .Select(m => new Row(byProject[m.ProjectId], m, MilestoneRules.Evaluate(m.Fields, today)))
            .OrderBy(r => r.Project.Fields.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Milestone, MilestoneRules.ProjectOrder)
            .ToList();
        return ExportTable.From(rows, Columns);
    }

    private static readonly ExportColumn<Row>[] Columns =
    [
        new("project_code", r => r.Project.Fields.Code),
        new("project_name", r => r.Project.Fields.Name),
        new("milestone_id", r => r.Milestone.Id.Value),
        new("name", r => r.Milestone.Fields.Name),
        new("category", r => r.Milestone.Fields.Category),
        new("planned_date", r => r.Milestone.Fields.PlannedDate),
        new("forecast_date", r => r.Milestone.Fields.ForecastDate),
        new("actual_date", r => r.Milestone.Fields.ActualDate),
        new("status", r => r.Evaluation.Status),
        new("slippage_days", r => r.Evaluation.SlippageDays),
        new("days_overdue", r => r.Evaluation.DaysOverdue),
        new("owner", r => r.Milestone.Fields.Owner),
        new("notes", r => r.Milestone.Fields.Notes),
        new("sort_order", r => r.Milestone.Fields.SortOrder),
        new("updated_at", r => r.Milestone.Updated.At),
    ];
}
