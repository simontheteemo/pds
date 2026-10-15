using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Costs.Contracts;
using PDS.Costs.Data;
using PDS.Costs.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared.Security;
using PDS.Shared.Settings;
using PDS.Shared.Web;

namespace PDS.Costs.Features;

internal static class UpdateBudgetLine
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPut("/lines/{lineId:guid}", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<UpdateBudgetLineRequest>()
            .WithName("UpdateBudgetLine");

    internal static async Task<Results<Ok<BudgetLineDetails>, NotFound, ValidationProblem, ProblemHttpResult>> Handle(
        Guid projectId, Guid lineId, UpdateBudgetLineRequest request, CostStore store, IPortfolioQueries portfolio,
        ICurrentUser user, TimeProvider clock, IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var project = await ProjectLookup.FindAsync(portfolio, projectId, ct);
        if (project is null)
            return TypedResults.NotFound();
        if (project.IsArchived)
            return CostsProblems.ProjectArchived();

        var pid = new ProjectId(projectId);
        var line = await store.GetLineAsync(pid, new BudgetLineId(lineId), ct);
        if (line is null)
            return TypedResults.NotFound();

        var key = BudgetLineRules.NameKey(request.Name!);
        var data = await store.LoadProjectAsync(pid, ct);
        if (data.Lines.Any(l => l.Id != line.Id && BudgetLineRules.NameKey(l.Fields.Name) == key))
            return CostsProblems.DuplicateLineName();
        if (line.Version != request.Version)
            return CostsProblems.VersionConflict("budget line");

        line.Update(request.ToFields(locale.Value.Currency), new AuditStamp(clock.GetUtcNow(), user.Name));
        return await store.UpdateLineAsync(line, request.Version, ct)
            ? TypedResults.Ok(BudgetLineDetails.From(line))
            : CostsProblems.VersionConflict("budget line");
    }
}
