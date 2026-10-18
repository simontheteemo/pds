using Microsoft.Extensions.Options;
using PDS.Portfolio.Contracts;
using PDS.Portfolio.Domain;
using PDS.Shared.Settings;

namespace PDS.Portfolio.Data;

internal sealed class PortfolioQueries(ProjectStore store, MilestoneStore milestones, TimeProvider clock, IOptions<LocaleOptions> locale) : IPortfolioQueries
{
    public async Task<bool> Exists(ProjectId id, CancellationToken ct) => await store.GetAsync(id, ct) is not null;

    public async Task<IReadOnlyList<ProjectSummary>> GetSummaries(IReadOnlyCollection<ProjectId> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
            return [];

        var projects = await store.GetManyAsync(ids, ct);
        return projects
            .Select(p => new ProjectSummary(p.Id, p.Fields.Code, p.Fields.Name, p.Fields.Stage.ToString(), p.Fields.Status.ToString(), p.IsArchived))
            .ToList();
    }

    public async Task<ProjectSchedule?> GetSchedule(ProjectId id, CancellationToken ct) =>
        await store.GetAsync(id, ct) is { } p
            ? new ProjectSchedule(p.Fields.PlannedStart, p.Fields.PlannedCompletion, p.Fields.ActualStart, p.Fields.ActualCompletion)
            : null;

    public async Task<IReadOnlyList<MilestoneSummary>> GetMilestones(ProjectId id, CancellationToken ct)
    {
        var today = LocalDate.Today(clock, locale.Value);
        var list = await milestones.ListForProjectAsync(id, ct);
        return list.Order(MilestoneRules.ProjectOrder).Select(m => ToSummary(m, today)).ToList();
    }

    public async Task<IReadOnlyList<MilestoneSummary>> GetOpenMilestones(CancellationToken ct)
    {
        var today = LocalDate.Today(clock, locale.Value);
        var live = (await store.ListAllAsync(ct)).Where(p => !p.IsArchived).Select(p => p.Id).ToHashSet();
        return (await milestones.ListAllAsync(ct))
            .Where(m => m.Fields.ActualDate is null && live.Contains(m.ProjectId))
            .OrderBy(m => MilestoneRules.DueDate(m.Fields) is null)
            .ThenBy(m => MilestoneRules.DueDate(m.Fields))
            .ThenBy(m => m.ProjectId.Value)
            .ThenBy(m => m.Fields.SortOrder)
            .Select(m => ToSummary(m, today))
            .ToList();
    }

    private static MilestoneSummary ToSummary(Milestone m, DateOnly today)
    {
        var f = m.Fields;
        var e = MilestoneRules.Evaluate(f, today);
        return new MilestoneSummary(
            m.Id, m.ProjectId, f.Name, f.Category.ToString(), f.PlannedDate, f.ForecastDate, f.ActualDate,
            e.Status.ToString(), e.SlippageDays, f.Owner, f.SortOrder);
    }
}
