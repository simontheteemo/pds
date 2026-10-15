using PDS.Costs.Contracts;
using PDS.Portfolio.Contracts;

namespace PDS.Costs.Domain;

internal sealed class ActualCost
{
    private ActualCost(
        ActualCostId id, ProjectId projectId, ActualCostFields fields, AuditStamp created, AuditStamp updated, long version)
    {
        Id = id;
        ProjectId = projectId;
        Fields = fields;
        Created = created;
        Updated = updated;
        Version = version;
    }

    public ActualCostId Id { get; }

    public ProjectId ProjectId { get; }

    public ActualCostFields Fields { get; private set; }

    public AuditStamp Created { get; }

    public AuditStamp Updated { get; private set; }

    public long Version { get; private set; }

    public static ActualCost Create(ProjectId projectId, ActualCostFields fields, AuditStamp stamp)
    {
        var normalised = CostEntryRules.Normalise(fields);
        CostEntryRules.EnsureValid(normalised);
        return new ActualCost(ActualCostId.New(), projectId, normalised, stamp, stamp, 1);
    }

    public static ActualCost Rehydrate(
        ActualCostId id, ProjectId projectId, ActualCostFields fields, AuditStamp created, AuditStamp updated, long version) =>
        new(id, projectId, fields, created, updated, version);

    public void Update(ActualCostFields fields, AuditStamp stamp)
    {
        var normalised = CostEntryRules.Normalise(fields);
        CostEntryRules.EnsureValid(normalised);
        Fields = normalised;
        Updated = stamp;
        Version++;
    }
}
