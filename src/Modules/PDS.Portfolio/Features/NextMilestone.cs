using PDS.Portfolio.Domain;

namespace PDS.Portfolio.Features;

internal sealed record NextMilestoneDto(Guid Id, string Name, DateOnly? Date, MilestoneStatus Status);

internal static class NextMilestone
{
    /// <summary>The earliest-due incomplete milestone (undated last), or null when none remain.</summary>
    public static NextMilestoneDto? For(IEnumerable<Milestone> projectMilestones, DateOnly today)
    {
        var next = projectMilestones
            .Where(m => m.Fields.ActualDate is null)
            .OrderBy(m => MilestoneRules.DueDate(m.Fields) is null)
            .ThenBy(m => MilestoneRules.DueDate(m.Fields))
            .ThenBy(m => m, MilestoneRules.ProjectOrder)
            .FirstOrDefault();
        return next is null
            ? null
            : new NextMilestoneDto(next.Id.Value, next.Fields.Name, MilestoneRules.DueDate(next.Fields), MilestoneRules.Evaluate(next.Fields, today).Status);
    }
}
