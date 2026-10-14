using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Costs.Data;
using PDS.Costs.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared;
using PDS.Shared.Security;
using PDS.Shared.Settings;

namespace PDS.Costs.Features;

internal static class AddStandardLines
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/lines/standard", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithName("AddStandardBudgetLines");

    /// <summary>Adds the configured standard lines that don't exist yet (matched by name). Returns only the added lines.</summary>
    internal static async Task<Results<Ok<List<BudgetLineDetails>>, NotFound, ValidationProblem>> Handle(
        Guid projectId, CostStore store, IPortfolioQueries portfolio, ICurrentUser user, TimeProvider clock,
        IOptions<LocaleOptions> locale, IOptions<CostsOptions> options, CancellationToken ct)
    {
        var project = await ProjectLookup.FindAsync(portfolio, projectId, ct);
        if (project is null)
            return TypedResults.NotFound();
        if (project.IsArchived)
            return CostsProblems.ProjectArchived();

        var pid = new ProjectId(projectId);
        var data = await store.LoadProjectAsync(pid, ct);
        var existing = data.Lines.Select(l => BudgetLineRules.NameKey(l.Fields.Name)).ToHashSet();
        var sortOrder = data.Lines.Count == 0 ? 10 : (data.Lines.Max(l => l.Fields.SortOrder) / 10 + 1) * 10;
        var stamp = new AuditStamp(clock.GetUtcNow(), user.Name);
        var zero = new Money(0m, locale.Value.Currency);

        var added = new List<BudgetLineDetails>();
        foreach (var name in options.Value.EffectiveStandardLines)
        {
            if (!existing.Add(BudgetLineRules.NameKey(name)))
                continue;
            var line = BudgetLine.Create(pid, new BudgetLineFields(name, sortOrder, zero, null, null), stamp);
            await store.CreateLineAsync(line, ct);
            added.Add(BudgetLineDetails.From(line));
            sortOrder += 10;
        }

        return TypedResults.Ok(added);
    }
}
