using PDS.Portfolio.Contracts;

namespace PDS.Portfolio.Domain;

internal sealed class Milestone
{
    private Milestone(
        MilestoneId id, ProjectId projectId, MilestoneFields fields, AuditStamp created, AuditStamp updated, long version)
    {
        Id = id;
        ProjectId = projectId;
        Fields = fields;
        Created = created;
        Updated = updated;
        Version = version;
    }

    public MilestoneId Id { get; }

    public ProjectId ProjectId { get; }

    public MilestoneFields Fields { get; private set; }

    public AuditStamp Created { get; }

    public AuditStamp Updated { get; private set; }

    public long Version { get; private set; }

    public static Milestone Create(ProjectId projectId, MilestoneFields fields, AuditStamp stamp)
    {
        var normalised = MilestoneRules.Normalise(fields);
        MilestoneRules.EnsureValid(normalised);
        return new Milestone(MilestoneId.New(), projectId, normalised, stamp, stamp, 1);
    }

    public static Milestone Rehydrate(
        MilestoneId id, ProjectId projectId, MilestoneFields fields, AuditStamp created, AuditStamp updated, long version) =>
        new(id, projectId, fields, created, updated, version);

    public void Update(MilestoneFields fields, AuditStamp stamp)
    {
        var normalised = MilestoneRules.Normalise(fields);
        MilestoneRules.EnsureValid(normalised);
        Fields = normalised;
        Updated = stamp;
        Version++;
    }
}
