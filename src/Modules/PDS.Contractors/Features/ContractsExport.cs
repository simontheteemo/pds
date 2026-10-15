using Microsoft.Extensions.Options;
using PDS.Contractors.Data;
using PDS.Contractors.Domain;
using PDS.Portfolio.Contracts;
using PDS.Reporting.Contracts;
using PDS.Shared.Settings;

namespace PDS.Contractors.Features;

internal sealed class ContractsExport(
    ContractStore contracts, ContractorStore contractors, IPortfolioQueries portfolio, IOptions<LocaleOptions> locale) : IExportDataset
{
    public string Name => "contracts";

    public string Title => "Contracts";

    public string Description => "Every contract in every project, with contractor, form, status, value and dates.";

    public async Task<ExportTable> LoadAsync(CancellationToken ct)
    {
        var currency = locale.Value.Currency;
        var all = await contracts.ScanAllAsync(ct);
        var names = (await contractors.GetManyAsync(all.Select(c => c.ContractorId).Distinct().ToList(), ct))
            .ToDictionary(c => c.Id, c => c.Fields.Name);
        var projects = (await portfolio.GetSummaries(all.Select(c => c.ProjectId).Distinct().ToList(), ct))
            .ToDictionary(p => p.Id);
        var rows = all
            .Select(c => new Row(c, projects.GetValueOrDefault(c.ProjectId), names.GetValueOrDefault(c.ContractorId)))
            .OrderBy(x => x.Project?.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ContractorName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Contract.Fields.Title, StringComparer.OrdinalIgnoreCase);

        return ExportTable.From<Row>(rows,
        [
            new("contract_id", x => x.Contract.Id.Value),
            new("project_id", x => x.Contract.ProjectId.Value),
            new("project_code", x => x.Project?.Code),
            new("project_name", x => x.Project?.Name),
            new("contractor_id", x => x.Contract.ContractorId.Value),
            new("contractor_name", x => x.ContractorName),
            new("title", x => x.Contract.Fields.Title),
            new("reference", x => x.Contract.Fields.Reference),
            new("form", x => x.Contract.Fields.Form),
            new("status", x => x.Contract.Fields.Status),
            new("value_amount", x => x.Contract.Fields.Value?.Amount),
            new("currency", x => x.Contract.Fields.Value?.Currency ?? currency),
            new("start_date", x => x.Contract.Fields.StartDate),
            new("end_date", x => x.Contract.Fields.EndDate),
            new("retention_percent", x => x.Contract.Fields.RetentionPercent),
            new("notes", x => x.Contract.Fields.Notes),
            new("created_at", x => x.Contract.Created.At),
            new("updated_at", x => x.Contract.Updated.At),
        ]);
    }

    private sealed record Row(Contract Contract, ProjectSummary? Project, string? ContractorName);
}
