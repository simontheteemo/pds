using PDS.Portfolio.Contracts;
using PDS.Reporting.Contracts;
using PDS.Risks.Data;
using PDS.Risks.Domain;

namespace PDS.Risks.Features;

internal sealed class RisksExport(RiskStore store, IPortfolioQueries portfolio) : IExportDataset
{
    public string Name => "risks";

    public string Title => "Risks";

    public string Description => "Every risk in every project, with its rating, score, band, owner and status.";

    public async Task<ExportTable> LoadAsync(CancellationToken ct)
    {
        var risks = await store.ScanAllAsync(ct);
        var projects = (await portfolio.GetSummaries(risks.Select(r => r.ProjectId).Distinct().ToList(), ct))
            .ToDictionary(p => p.Id);
        var rows = risks
            .Select(r => (Risk: r, Project: projects.GetValueOrDefault(r.ProjectId)))
            .OrderBy(x => x.Project?.Code, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(x => x.Risk.Score)
            .ThenBy(x => x.Risk.Fields.Title, StringComparer.OrdinalIgnoreCase);

        return ExportTable.From<(Risk Risk, ProjectSummary? Project)>(rows,
        [
            new("risk_id", x => x.Risk.Id.Value),
            new("project_id", x => x.Risk.ProjectId.Value),
            new("project_code", x => x.Project?.Code),
            new("project_name", x => x.Project?.Name),
            new("title", x => x.Risk.Fields.Title),
            new("category", x => x.Risk.Fields.Category),
            new("likelihood", x => x.Risk.Fields.Likelihood),
            new("impact", x => x.Risk.Fields.Impact),
            new("score", x => x.Risk.Score),
            new("band", x => x.Risk.Band),
            new("status", x => x.Risk.Status),
            new("owner", x => x.Risk.Fields.Owner),
            new("due_date", x => x.Risk.Fields.DueDate),
            new("description", x => x.Risk.Fields.Description),
            new("mitigation", x => x.Risk.Fields.Mitigation),
            new("closing_note", x => x.Risk.ClosingNote),
            new("closed_at", x => x.Risk.Closed?.At),
            new("created_at", x => x.Risk.Created.At),
            new("updated_at", x => x.Risk.Updated.At),
        ]);
    }
}
