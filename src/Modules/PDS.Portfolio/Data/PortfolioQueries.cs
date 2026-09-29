using PDS.Portfolio.Contracts;

namespace PDS.Portfolio.Data;

internal sealed class PortfolioQueries(ProjectStore store) : IPortfolioQueries
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
}
