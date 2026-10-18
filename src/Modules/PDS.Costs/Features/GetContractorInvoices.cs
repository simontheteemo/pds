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

internal static class GetContractorInvoices
{
    private const string DeletedLine = "(deleted line)";

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/invoices", Handle).WithName("GetContractorInvoices");

    internal static async Task<Results<Ok<ContractorInvoices>, NotFound>> Handle(
        Guid contractorId, CostStore store, IPortfolioQueries portfolio, IContractQueries contractQueries,
        IContractorQueries contractorQueries, IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var cid = new ContractorId(contractorId);
        var contractor = (await contractorQueries.GetContractors([cid], ct)).FirstOrDefault();
        if (contractor is null)
            return TypedResults.NotFound();

        var actuals = await store.ListActualsForContractorAsync(cid, ct);
        var contracts = await contractQueries.GetContractorContracts(cid, ct);

        var projectIds = actuals.Select(a => a.ProjectId)
            .Concat(contracts.Select(c => c.ProjectId))
            .Distinct()
            .ToList();
        var projects = projectIds.Count == 0
            ? new Dictionary<ProjectId, ProjectSummary>()
            : (await portfolio.GetSummaries(projectIds, ct)).ToDictionary(p => p.Id);

        var lineNames = new Dictionary<BudgetLineId, string>();
        foreach (var pid in actuals.Select(a => a.ProjectId).Distinct())
            foreach (var line in await store.ListLinesAsync(pid, ct))
                lineNames[line.Id] = line.Fields.Name;

        var contractById = contracts.ToDictionary(c => c.Id);
        var invoicedByContract = actuals
            .Where(a => a.Fields.ContractId is not null)
            .GroupBy(a => a.Fields.ContractId!.Value)
            .ToDictionary(g => g.Key, g => decimal.Round(g.Sum(a => a.Fields.Amount.Amount), 2));

        decimal SumAmount(Func<ActualCost, bool> filter) =>
            decimal.Round(actuals.Where(filter).Sum(a => a.Fields.Amount.Amount), 2);
        var invoiced = SumAmount(_ => true);
        var paid = SumAmount(a => a.Fields.Status == InvoiceStatus.Paid);
        var totals = new ContractorInvoiceTotals(
            invoiced, paid, decimal.Round(invoiced - paid, 2),
            SumAmount(a => a.Fields.Status == InvoiceStatus.Disputed),
            decimal.Round(actuals.Sum(a => a.Fields.Gst?.Amount ?? 0m), 2));

        var invoices = actuals
            .OrderByDescending(a => a.Fields.Date)
            .ThenBy(a => a.Fields.Description, StringComparer.OrdinalIgnoreCase)
            .Select(a =>
            {
                var project = projects.GetValueOrDefault(a.ProjectId);
                var title = a.Fields.ContractId is { } kid && contractById.TryGetValue(kid, out var c) ? c.Title : null;
                return new ContractorInvoice(
                    ActualCostDetails.From(a), a.ProjectId.Value, project?.Code, project?.Name, project?.IsArchived ?? false,
                    lineNames.GetValueOrDefault(a.Fields.BudgetLineId, DeletedLine), title);
            })
            .ToList();

        var contractRows = contracts
            .Select(c =>
            {
                var project = projects.GetValueOrDefault(c.ProjectId);
                var toDate = invoicedByContract.GetValueOrDefault(c.Id, 0m);
                return new ContractorInvoiceContract(
                    c.Id.Value, c.ProjectId.Value, project?.Code, project?.Name, c.Title, c.Status, c.ValueAmount,
                    toDate, c.ValueAmount is { } v ? decimal.Round(v - toDate, 2) : null);
            })
            .OrderBy(c => c.ProjectCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return TypedResults.Ok(new ContractorInvoices(
            contractorId, contractor.Name, contractor.IsActive, locale.Value.Currency, totals, invoices, contractRows));
    }
}
