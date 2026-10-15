using Microsoft.Extensions.Options;
using PDS.Portfolio.Data;
using PDS.Portfolio.Domain;
using PDS.Reporting.Contracts;
using PDS.Shared.Settings;

namespace PDS.Portfolio.Features;

internal sealed class ProjectsExport(ProjectStore store, IOptions<LocaleOptions> locale) : IExportDataset
{
    public string Name => "projects";

    public string Title => "Projects";

    public string Description => "Every project with its stage, status, site, dates and budget.";

    public async Task<ExportTable> LoadAsync(CancellationToken ct)
    {
        var projects = (await store.ListAllAsync(ct)).OrderBy(p => p.Fields.Code, StringComparer.OrdinalIgnoreCase);
        return ExportTable.From(projects, Columns(locale.Value.Currency));
    }

    private static ExportColumn<Project>[] Columns(string currency) =>
    [
        new("project_id", p => p.Id.Value),
        new("project_code", p => p.Fields.Code),
        new("project_name", p => p.Fields.Name),
        new("stage", p => p.Fields.Stage),
        new("status", p => p.Fields.Status),
        new("archived", p => p.IsArchived),
        new("address_line", p => p.Fields.Site.AddressLine),
        new("suburb", p => p.Fields.Site.Suburb),
        new("city", p => p.Fields.Site.City),
        new("region", p => p.Fields.Site.Region),
        new("postcode", p => p.Fields.Site.Postcode),
        new("legal_description", p => p.Fields.Site.LegalDescription),
        new("title_reference", p => p.Fields.Site.TitleReference),
        new("land_area_sqm", p => p.Fields.Site.LandAreaSqm),
        new("planned_start", p => p.Fields.PlannedStart),
        new("planned_completion", p => p.Fields.PlannedCompletion),
        new("actual_start", p => p.Fields.ActualStart),
        new("actual_completion", p => p.Fields.ActualCompletion),
        new("budget_amount", p => p.Fields.Budget?.Amount),
        new("currency", p => p.Fields.Budget?.Currency ?? currency),
        new("project_manager", p => p.Fields.ProjectManager),
        new("description", p => p.Fields.Description),
        new("created_at", p => p.Created.At),
        new("updated_at", p => p.Updated.At),
    ];
}
