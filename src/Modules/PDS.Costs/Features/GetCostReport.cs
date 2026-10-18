using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Contractors.Contracts;
using PDS.Costs.Contracts;
using PDS.Costs.Data;
using PDS.Costs.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared.Settings;

namespace PDS.Costs.Features;

internal static class GetCostReport
{
    private const string DeletedLine = "(deleted line)";
    private const string UnknownContract = "(unknown contract)";

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("", Handle).WithName("GetCostReport");

    internal static async Task<Results<Ok<ProjectCostReport>, NotFound>> Handle(
        Guid projectId, CostStore store, IPortfolioQueries portfolio, IContractQueries contractQueries,
        IContractorQueries contractorQueries, IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var project = await ProjectLookup.FindAsync(portfolio, projectId, ct);
        if (project is null)
            return TypedResults.NotFound();

        var pid = new ProjectId(projectId);
        var data = await store.LoadProjectAsync(pid, ct);
        var contracts = await contractQueries.GetProjectContracts(pid, ct);
        var (figures, total) = CostCalculator.For(data.Lines, data.Commitments, data.Actuals);

        var lines = data.Lines
            .OrderBy(l => l.Fields.SortOrder)
            .ThenBy(l => l.Fields.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var lineNames = lines.ToDictionary(l => l.Id, l => l.Fields.Name);
        var contractById = contracts.ToDictionary(c => c.Id);
        var contractorIds = data.Actuals
            .Where(a => a.Fields.ContractorId is not null)
            .Select(a => a.Fields.ContractorId!.Value)
            .Distinct()
            .ToList();
        var contractorNames = contractorIds.Count == 0
            ? new Dictionary<ContractorId, string>()
            : (await contractorQueries.GetContractors(contractorIds, ct)).ToDictionary(c => c.Id, c => c.Name);
        var invoicedByContract = data.Actuals
            .Where(a => a.Fields.ContractId is not null)
            .GroupBy(a => a.Fields.ContractId!.Value)
            .ToDictionary(g => g.Key, g => decimal.Round(g.Sum(a => a.Fields.Amount.Amount), 2));

        // An entry can reference a line deleted in a race the spec accepts; it then counts toward no line.
        string LineName(BudgetLineId id) => lineNames.GetValueOrDefault(id, DeletedLine);
        (string? Title, string? Contractor) ContractNames(ContractId? id) =>
            id is not { } cid ? (null, null)
            : contractById.TryGetValue(cid, out var c) ? (c.Title, c.ContractorName)
            : (UnknownContract, null);

        var commitments = data.Commitments
            .OrderByDescending(c => c.Fields.Date)
            .ThenBy(c => c.Fields.Description, StringComparer.OrdinalIgnoreCase)
            .Select(c =>
            {
                var (title, contractor) = ContractNames(c.Fields.ContractId);
                return new CostReportCommitment(CommitmentDetails.From(c), LineName(c.Fields.BudgetLineId), title, contractor);
            })
            .ToList();

        var actuals = data.Actuals
            .OrderByDescending(a => a.Fields.Date)
            .ThenBy(a => a.Fields.Description, StringComparer.OrdinalIgnoreCase)
            .Select(a =>
            {
                var (title, contractContractor) = ContractNames(a.Fields.ContractId);
                var contractor = a.Fields.ContractorId is { } kid && contractorNames.TryGetValue(kid, out var name)
                    ? name
                    : contractContractor;
                return new CostReportActual(ActualCostDetails.From(a), LineName(a.Fields.BudgetLineId), title, contractor);
            })
            .ToList();

        var report = new ProjectCostReport(
            projectId, project.Code, project.Name, project.IsArchived, locale.Value.Currency,
            CostFiguresDto.From(total),
            lines.Select(l => new CostReportLine(BudgetLineDetails.From(l), CostFiguresDto.From(figures[l.Id]))).ToList(),
            commitments,
            actuals,
            contracts
                .OrderBy(c => c.ContractorName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.Title, StringComparer.OrdinalIgnoreCase)
                .Select(c => new CostReportContract(
                    c.Id.Value, c.ContractorId.Value, c.ContractorName, c.Title, c.Reference, c.Status, c.ValueAmount,
                    invoicedByContract.GetValueOrDefault(c.Id, 0m)))
                .ToList());
        return TypedResults.Ok(report);
    }
}
