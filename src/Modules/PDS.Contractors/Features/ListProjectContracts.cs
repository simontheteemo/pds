using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Contractors.Data;
using PDS.Contractors.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared.Settings;

namespace PDS.Contractors.Features;

internal static class ListProjectContracts
{
    private static readonly Dictionary<ContractStatus, int> StatusRank = new()
    {
        [ContractStatus.Active] = 0,
        [ContractStatus.PracticalCompletion] = 1,
        [ContractStatus.Draft] = 2,
        [ContractStatus.Complete] = 3,
        [ContractStatus.Terminated] = 4,
    };

    public static void Map(RouteGroupBuilder group) => group.MapGet("", Handle).WithName("ListProjectContracts");

    internal static async Task<Results<Ok<List<ContractListItem>>, NotFound>> Handle(
        Guid projectId, ContractStore store, ContractorStore contractors, IPortfolioQueries portfolio,
        IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var project = await ProjectLookup.FindAsync(portfolio, projectId, ct);
        if (project is null)
            return TypedResults.NotFound();

        var list = await store.ListForProjectAsync(new ProjectId(projectId), ct);
        var names = (await contractors.GetManyAsync(list.Select(c => c.ContractorId).Distinct().ToList(), ct))
            .ToDictionary(c => c.Id, c => c.Fields.Name);

        var items = list
            .OrderBy(c => StatusRank[c.Fields.Status])
            .ThenBy(c => c.Fields.StartDate is null)
            .ThenBy(c => c.Fields.StartDate)
            .ThenBy(c => c.Fields.Title, StringComparer.OrdinalIgnoreCase)
            .Select(c => ContractListing.ToListItem(
                c, names.GetValueOrDefault(c.ContractorId, ContractListing.UnknownContractor), project.Code, project.Name, locale.Value.Currency))
            .ToList();
        return TypedResults.Ok(items);
    }
}
