using PDS.Portfolio.Contracts;

namespace PDS.Portfolio.Domain;

internal sealed class Project
{
    private Project(ProjectId id, ProjectFields fields, bool isArchived, AuditStamp created, AuditStamp updated, long version)
    {
        Id = id;
        Fields = fields;
        IsArchived = isArchived;
        Created = created;
        Updated = updated;
        Version = version;
    }

    public ProjectId Id { get; }

    public ProjectFields Fields { get; private set; }

    public bool IsArchived { get; private set; }

    public AuditStamp Created { get; }

    public AuditStamp Updated { get; private set; }

    /// <summary>Starts at 1 and increases by 1 on every change; used for optimistic concurrency.</summary>
    public long Version { get; private set; }

    public static Project Create(ProjectFields fields, AuditStamp stamp)
    {
        var normalised = ProjectRules.Normalise(fields);
        ProjectRules.EnsureValid(normalised);
        return new Project(ProjectId.New(), normalised, false, stamp, stamp, 1);
    }

    public static Project Rehydrate(
        ProjectId id, ProjectFields fields, bool isArchived, AuditStamp created, AuditStamp updated, long version) =>
        new(id, fields, isArchived, created, updated, version);

    public void Update(ProjectFields fields, AuditStamp stamp)
    {
        var normalised = ProjectRules.Normalise(fields);
        ProjectRules.EnsureValid(normalised);
        Fields = normalised;
        Touch(stamp);
    }

    /// <returns>False when the project was already in the requested state (nothing changed).</returns>
    public bool SetArchived(bool archived, AuditStamp stamp)
    {
        if (IsArchived == archived)
            return false;

        IsArchived = archived;
        Touch(stamp);
        return true;
    }

    private void Touch(AuditStamp stamp)
    {
        Updated = stamp;
        Version++;
    }
}
