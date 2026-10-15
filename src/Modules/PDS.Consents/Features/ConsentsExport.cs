using PDS.Consents.Data;
using PDS.Portfolio.Contracts;
using PDS.Reporting.Contracts;

namespace PDS.Consents.Features;

internal sealed class ConsentsExport(ConsentStore store, IPortfolioQueries portfolio) : IExportDataset
{
    public string Name => "consents";

    public string Title => "Consents";

    public string Description => "Every consent in every project with its authority, status, key dates and condition counts.";

    public async Task<ExportTable> LoadAsync(CancellationToken ct)
    {
        var all = await store.ScanAllAsync(ct);
        var projects = (await portfolio.GetSummaries(all.Select(r => r.Consent.ProjectId).Distinct().ToList(), ct))
            .ToDictionary(p => p.Id);
        var rows = all
            .Select(r => (Record: r, Project: projects.GetValueOrDefault(r.Consent.ProjectId)))
            .OrderBy(x => x.Project?.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Record.Consent.Fields.Title, StringComparer.OrdinalIgnoreCase);

        return ExportTable.From<(ConsentWithConditions Record, ProjectSummary? Project)>(rows,
        [
            new("consent_id", x => x.Record.Consent.Id.Value),
            new("project_id", x => x.Record.Consent.ProjectId.Value),
            new("project_code", x => x.Project?.Code),
            new("project_name", x => x.Project?.Name),
            new("type", x => x.Record.Consent.Fields.Type),
            new("title", x => x.Record.Consent.Fields.Title),
            new("authority", x => x.Record.Consent.Fields.Authority),
            new("reference", x => x.Record.Consent.Fields.Reference),
            new("status", x => x.Record.Consent.Fields.Status),
            new("lodged_date", x => x.Record.Consent.Fields.LodgedDate),
            new("granted_date", x => x.Record.Consent.Fields.GrantedDate),
            new("expiry_date", x => x.Record.Consent.Fields.ExpiryDate),
            new("condition_count", x => x.Record.Conditions.Count),
            new("open_condition_count", x => x.Record.Conditions.Count(c => c.CompletedOn is null)),
            new("notes", x => x.Record.Consent.Fields.Notes),
            new("created_at", x => x.Record.Consent.Created.At),
            new("updated_at", x => x.Record.Consent.Updated.At),
        ]);
    }
}
