using PDS.Costs.Contracts;
using PDS.Portfolio.Contracts;

namespace PDS.Costs.Domain;

internal sealed class Commitment
{
    private Commitment(
        CommitmentId id, ProjectId projectId, CommitmentFields fields, AuditStamp created, AuditStamp updated, long version)
    {
        Id = id;
        ProjectId = projectId;
        Fields = fields;
        Created = created;
        Updated = updated;
        Version = version;
    }

    public CommitmentId Id { get; }

    public ProjectId ProjectId { get; }

    public CommitmentFields Fields { get; private set; }

    public AuditStamp Created { get; }

    public AuditStamp Updated { get; private set; }

    public long Version { get; private set; }

    public static Commitment Create(ProjectId projectId, CommitmentFields fields, AuditStamp stamp)
    {
        var normalised = CostEntryRules.Normalise(fields);
        CostEntryRules.EnsureValid(normalised);
        return new Commitment(CommitmentId.New(), projectId, normalised, stamp, stamp, 1);
    }

    public static Commitment Rehydrate(
        CommitmentId id, ProjectId projectId, CommitmentFields fields, AuditStamp created, AuditStamp updated, long version) =>
        new(id, projectId, fields, created, updated, version);

    public void Update(CommitmentFields fields, AuditStamp stamp)
    {
        var normalised = CostEntryRules.Normalise(fields);
        CostEntryRules.EnsureValid(normalised);
        Fields = normalised;
        Updated = stamp;
        Version++;
    }
}
