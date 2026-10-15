using Microsoft.Extensions.Options;
using PDS.Consents.Data;
using PDS.Consents.Domain;
using PDS.Portfolio.Contracts;
using PDS.Reporting.Contracts;
using PDS.Shared.Settings;

namespace PDS.Consents.Features;

internal sealed class ConsentConditionsExport(
    ConsentStore store, IPortfolioQueries portfolio, TimeProvider clock, IOptions<LocaleOptions> locale) : IExportDataset
{
    public string Name => "consent-conditions";

    public string Title => "Consent conditions";

    public string Description => "Every consent condition with its due date, completion and due state as of today.";

    public async Task<ExportTable> LoadAsync(CancellationToken ct)
    {
        var today = LocalDate.Today(clock, locale.Value);
        var all = await store.ScanAllAsync(ct);
        var projects = (await portfolio.GetSummaries(all.Select(r => r.Consent.ProjectId).Distinct().ToList(), ct))
            .ToDictionary(p => p.Id);
        var rows = all
            .SelectMany(r => r.Conditions.Select(c => new Row(
                c, r.Consent, projects.GetValueOrDefault(r.Consent.ProjectId),
                ConsentRules.ConditionState(c, r.Consent.IsActive, today))))
            .OrderBy(x => x.Project?.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Consent.Fields.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Condition.Fields.DueDate is null)
            .ThenBy(x => x.Condition.Fields.DueDate)
            .ThenBy(x => x.Condition.Fields.Reference, StringComparer.OrdinalIgnoreCase);

        return ExportTable.From<Row>(rows,
        [
            new("condition_id", x => x.Condition.Id.Value),
            new("consent_id", x => x.Consent.Id.Value),
            new("project_id", x => x.Consent.ProjectId.Value),
            new("project_code", x => x.Project?.Code),
            new("project_name", x => x.Project?.Name),
            new("consent_type", x => x.Consent.Fields.Type),
            new("consent_title", x => x.Consent.Fields.Title),
            new("consent_reference", x => x.Consent.Fields.Reference),
            new("consent_status", x => x.Consent.Fields.Status),
            new("reference", x => x.Condition.Fields.Reference),
            new("description", x => x.Condition.Fields.Description),
            new("due_date", x => x.Condition.Fields.DueDate),
            new("completed_on", x => x.Condition.CompletedOn),
            new("due_state", x => x.DueState),
            new("created_at", x => x.Condition.Created.At),
            new("updated_at", x => x.Condition.Updated.At),
        ]);
    }

    private sealed record Row(Condition Condition, Consent Consent, ProjectSummary? Project, DueState DueState);
}
