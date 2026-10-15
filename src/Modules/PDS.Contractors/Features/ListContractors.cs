using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Contractors.Data;
using PDS.Contractors.Domain;
using PDS.Shared.Settings;

namespace PDS.Contractors.Features;

internal static class ListContractors
{
    public static void Map(RouteGroupBuilder group) => group.MapGet("", Handle).WithName("ListContractors");

    internal static async Task<Results<Ok<List<ContractorListItem>>, ValidationProblem>> Handle(
        string? search, ContractorType? type, bool? includeInactive, ContractorStore store, TimeProvider clock,
        IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        if (type is { } t && !Enum.IsDefined(t))
            return ContractorsProblems.InvalidType();

        var today = LocalDate.Today(clock, locale.Value);
        var term = search?.Trim();
        var items = (await store.ListAllAsync(ct))
            .Where(c => includeInactive == true || c.IsActive)
            .Where(c => type is null || c.Fields.Type == type)
            .Where(c => string.IsNullOrEmpty(term) || Matches(c.Fields, term))
            .OrderBy(c => c.Fields.Name, StringComparer.OrdinalIgnoreCase)
            .Select(c => new ContractorListItem(
                c.Id.Value, c.Fields.Name, c.Fields.Type, c.Fields.Trade, c.ComplianceOn(today), c.IsActive))
            .ToList();
        return TypedResults.Ok(items);
    }

    private static bool Matches(ContractorFields f, string term) =>
        Contains(f.Name, term) || Contains(f.Trade, term) || Contains(f.ContactName, term);

    private static bool Contains(string? value, string term) =>
        value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;
}
