using PDS.Contractors.Contracts;
using PDS.Costs.Contracts;
using PDS.Costs.Data;
using PDS.Portfolio.Contracts;
using PDS.Reporting.Contracts;

namespace PDS.Costs.Features;

internal sealed class CostEntriesExport(CostStore store, IPortfolioQueries portfolio, IContractQueries contracts) : IExportDataset
{
    private const string DeletedLine = "(deleted line)";
    private const string UnknownContract = "(unknown contract)";

    public string Name => "cost-entries";

    public string Title => "Commitments and actuals";

    public string Description => "Every commitment and actual cost in every project, with its budget line and contract (excl. GST).";

    public async Task<ExportTable> LoadAsync(CancellationToken ct)
    {
        var all = await store.ScanAllAsync(ct);
        var projects = (await portfolio.GetSummaries(all.Keys.ToList(), ct)).ToDictionary(p => p.Id);
        var entries = new List<Entry>();
        foreach (var (projectId, data) in all)
        {
            var project = projects.GetValueOrDefault(projectId);
            var contractById = (await contracts.GetProjectContracts(projectId, ct)).ToDictionary(c => c.Id);
            var lineNames = data.Lines.ToDictionary(l => l.Id, l => l.Fields.Name);

            string LineName(BudgetLineId id) => lineNames.GetValueOrDefault(id, DeletedLine);
            (string? Title, string? Contractor) ContractNames(ContractId? id) =>
                id is not { } cid ? (null, null)
                : contractById.TryGetValue(cid, out var c) ? (c.Title, c.ContractorName)
                : (UnknownContract, null);

            foreach (var c in data.Commitments)
            {
                var f = c.Fields;
                var (title, contractor) = ContractNames(f.ContractId);
                entries.Add(new Entry(
                    c.Id.Value, "Commitment", projectId, project, f.BudgetLineId.Value, LineName(f.BudgetLineId),
                    f.Type.ToString(), f.ContractId?.Value, title, contractor, null,
                    f.Description, f.Reference, f.Date, f.Amount.Amount, f.Amount.Currency, f.Notes, c.Created.At));
            }

            foreach (var a in data.Actuals)
            {
                var f = a.Fields;
                var (title, contractor) = ContractNames(f.ContractId);
                entries.Add(new Entry(
                    a.Id.Value, "Actual", projectId, project, f.BudgetLineId.Value, LineName(f.BudgetLineId),
                    f.Type.ToString(), f.ContractId?.Value, title, contractor, f.Payee,
                    f.Description, f.Reference, f.Date, f.Amount.Amount, f.Amount.Currency, f.Notes, a.Created.At));
            }
        }

        var ordered = entries
            .OrderBy(x => x.Project?.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Date)
            .ThenBy(x => x.EntryKind, StringComparer.Ordinal);

        return ExportTable.From<Entry>(ordered,
        [
            new("entry_id", x => x.EntryId),
            new("entry_kind", x => x.EntryKind),
            new("project_id", x => x.ProjectId.Value),
            new("project_code", x => x.Project?.Code),
            new("project_name", x => x.Project?.Name),
            new("budget_line_id", x => x.BudgetLineId),
            new("budget_line_name", x => x.BudgetLineName),
            new("type", x => x.Type),
            new("contract_id", x => x.ContractId),
            new("contract_title", x => x.ContractTitle),
            new("contractor_name", x => x.ContractorName),
            new("payee", x => x.Payee),
            new("description", x => x.Description),
            new("reference", x => x.Reference),
            new("date", x => x.Date),
            new("amount", x => x.Amount),
            new("currency", x => x.Currency),
            new("notes", x => x.Notes),
            new("created_at", x => x.CreatedAt),
        ]);
    }

    private sealed record Entry(
        Guid EntryId, string EntryKind, ProjectId ProjectId, ProjectSummary? Project, Guid BudgetLineId, string BudgetLineName,
        string Type, Guid? ContractId, string? ContractTitle, string? ContractorName, string? Payee,
        string Description, string? Reference, DateOnly Date, decimal Amount, string Currency, string? Notes, DateTimeOffset CreatedAt);
}
