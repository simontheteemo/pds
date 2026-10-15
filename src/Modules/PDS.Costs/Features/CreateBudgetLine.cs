using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Costs.Data;
using PDS.Costs.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared.Security;
using PDS.Shared.Settings;
using PDS.Shared.Web;

namespace PDS.Costs.Features;

internal static class CreateBudgetLine
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/lines", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<CreateBudgetLineRequest>()
            .WithName("CreateBudgetLine");

    internal static async Task<Results<Created<BudgetLineDetails>, NotFound, ValidationProblem>> Handle(
        Guid projectId, CreateBudgetLineRequest request, CostStore store, IPortfolioQueries portfolio,
        ICurrentUser user, TimeProvider clock, IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var project = await ProjectLookup.FindAsync(portfolio, projectId, ct);
        if (project is null)
            return TypedResults.NotFound();
        if (project.IsArchived)
            return CostsProblems.ProjectArchived();

        var pid = new ProjectId(projectId);
        var key = BudgetLineRules.NameKey(request.Name!);
        var data = await store.LoadProjectAsync(pid, ct);
        if (data.Lines.Any(l => BudgetLineRules.NameKey(l.Fields.Name) == key))
            return CostsProblems.DuplicateLineName();

        var line = BudgetLine.Create(pid, request.ToFields(locale.Value.Currency), new AuditStamp(clock.GetUtcNow(), user.Name));
        await store.CreateLineAsync(line, ct);
        return TypedResults.Created($"/api/costs/projects/{projectId}", BudgetLineDetails.From(line));
    }
}
