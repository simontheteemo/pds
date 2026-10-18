using PDS.Actions.Domain;
using PDS.Portfolio.Contracts;

namespace PDS.Actions.Features;

/// <summary>An action with its project, for cross-project lists.</summary>
internal sealed record ActionSummary(
    Guid Id,
    Guid ProjectId,
    string ProjectCode,
    string ProjectName,
    string Title,
    string OwnerId,
    string OwnerName,
    DateOnly? DueDate,
    ActionPriority Priority,
    ActionStatus Status,
    bool IsOverdue,
    ActionLinkDto? Link,
    long Version)
{
    public static ActionSummary From(ActionItem a, ProjectSummary project, DateOnly today)
    {
        var f = a.Fields;
        return new ActionSummary(
            a.Id.Value, a.ProjectId.Value, project.Code, project.Name, f.Title, f.OwnerId, f.OwnerName, f.DueDate,
            f.Priority, a.Status, a.IsOverdue(today),
            f.Link is { } link ? new ActionLinkDto(link.Module, link.RecordId, link.Label) : null, a.Version);
    }

    /// <summary>Pairs each action with its project, dropping actions whose project is missing or archived.</summary>
    public static async Task<List<ActionSummary>> JoinProjects(
        IPortfolioQueries portfolio, IEnumerable<ActionItem> actions, DateOnly today, CancellationToken ct)
    {
        var list = actions.ToList();
        if (list.Count == 0)
            return [];

        var ids = list.Select(a => a.ProjectId).Distinct().ToList();
        var projects = (await portfolio.GetSummaries(ids, ct)).Where(p => !p.IsArchived).ToDictionary(p => p.Id);
        return list
            .Where(a => projects.ContainsKey(a.ProjectId))
            .Select(a => From(a, projects[a.ProjectId], today))
            .ToList();
    }
}
