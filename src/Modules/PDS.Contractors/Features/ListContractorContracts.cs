using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Contractors.Contracts;
using PDS.Contractors.Data;
using PDS.Portfolio.Contracts;
using PDS.Shared.Settings;

namespace PDS.Contractors.Features;

internal static class ListContractorContracts
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{contractorId:guid}/contracts", Handle).WithName("ListContractorContracts");

    internal static async Task<Results<Ok<List<ContractListItem>>, NotFound>> Handle(
        Guid contractorId, ContractStore store, ContractorStore contractors, IPortfolioQueries portfolio,
        IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var contractor = await contractors.GetAsync(new ContractorId(contractorId), ct);
        if (contractor is null)
            return TypedResults.NotFound();

        var list = await store.ListForContractorAsync(contractor.Id, ct);
        var projects = (await portfolio.GetSummaries(list.Select(c => c.ProjectId).Distinct().ToList(), ct))
            .ToDictionary(p => p.Id);

        var items = list
            .OrderBy(c => c.Fields.StartDate is null)
            .ThenByDescending(c => c.Fields.StartDate)
            .ThenBy(c => c.Fields.Title, StringComparer.OrdinalIgnoreCase)
            .Select(c =>
            {
                projects.TryGetValue(c.ProjectId, out var p);
                return ContractListing.ToListItem(c, contractor.Fields.Name, p?.Code, p?.Name, locale.Value.Currency);
            })
            .ToList();
        return TypedResults.Ok(items);
    }
}
