using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Portfolio.Data;
using PDS.Portfolio.Domain;
using PDS.Shared.Settings;

namespace PDS.Portfolio.Features;

internal sealed record StageCount(ProjectStage Stage, int Count);

internal sealed record PortfolioSummary(
    int ActiveProjects, List<StageCount> ByStage, decimal TotalBudget, string Currency, int ProjectsSlipping, int OverdueMilestones);

internal static class GetPortfolioSummary
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/summary", Handle).WithName("GetPortfolioSummary");

    internal static async Task<Ok<PortfolioSummary>> Handle(
        ProjectStore projects, MilestoneStore milestones, TimeProvider clock, IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var today = LocalDate.Today(clock, locale.Value);
        var live = (await projects.ListAllAsync(ct)).Where(p => !p.IsArchived).ToList();
        var active = live.Where(p => p.Fields.Stage != ProjectStage.Completed && p.Fields.Status != ProjectStatus.Cancelled).ToList();
        var activeIds = active.Select(p => p.Id).ToHashSet();

        var byStage = live
            .GroupBy(p => p.Fields.Stage)
            .OrderBy(g => g.Key)
            .Select(g => new StageCount(g.Key, g.Count()))
            .ToList();

        var evaluated = (await milestones.ListAllAsync(ct))
            .Where(m => activeIds.Contains(m.ProjectId))
            .Select(m => (m.ProjectId, MilestoneRules.Evaluate(m.Fields, today).Status))
            .ToList();

        return TypedResults.Ok(new PortfolioSummary(
            active.Count,
            byStage,
            active.Sum(p => p.Fields.Budget?.Amount ?? 0m),
            locale.Value.Currency,
            evaluated.Where(e => MilestoneRules.IsSlipping(e.Status)).Select(e => e.ProjectId).Distinct().Count(),
            evaluated.Count(e => e.Status == MilestoneStatus.Overdue)));
    }
}
