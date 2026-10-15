using PDS.Consents.Contracts;
using PDS.Portfolio.Contracts;

namespace PDS.Consents.Domain;

internal sealed class Condition
{
    private Condition(
        ConditionId id, ProjectId projectId, ConsentId consentId, ConditionFields fields, DateOnly? completedOn,
        AuditStamp? completed, AuditStamp created, AuditStamp updated, long version)
    {
        Id = id;
        ProjectId = projectId;
        ConsentId = consentId;
        Fields = fields;
        CompletedOn = completedOn;
        Completed = completed;
        Created = created;
        Updated = updated;
        Version = version;
    }

    public ConditionId Id { get; }

    public ProjectId ProjectId { get; }

    public ConsentId ConsentId { get; }

    public ConditionFields Fields { get; private set; }

    public DateOnly? CompletedOn { get; private set; }

    public AuditStamp? Completed { get; private set; }

    public AuditStamp Created { get; }

    public AuditStamp Updated { get; private set; }

    public long Version { get; private set; }

    public static Condition Create(ProjectId projectId, ConsentId consentId, ConditionFields fields, AuditStamp stamp)
    {
        var normalised = ConsentRules.Normalise(fields);
        ConsentRules.EnsureValid(normalised);
        return new Condition(ConditionId.New(), projectId, consentId, normalised, null, null, stamp, stamp, 1);
    }

    public static Condition Rehydrate(
        ConditionId id, ProjectId projectId, ConsentId consentId, ConditionFields fields, DateOnly? completedOn,
        AuditStamp? completed, AuditStamp created, AuditStamp updated, long version) =>
        new(id, projectId, consentId, fields, completedOn, completed, created, updated, version);

    /// <summary>Edits fields; allowed whether or not the condition is complete.</summary>
    public void Update(ConditionFields fields, AuditStamp stamp)
    {
        var normalised = ConsentRules.Normalise(fields);
        ConsentRules.EnsureValid(normalised);
        Fields = normalised;
        Touch(stamp);
    }

    /// <returns>False when the condition was already complete (nothing changed).</returns>
    public bool Complete(DateOnly completedOn, AuditStamp stamp)
    {
        if (CompletedOn is not null)
            return false;

        CompletedOn = completedOn;
        Completed = stamp;
        Touch(stamp);
        return true;
    }

    /// <returns>False when the condition was not complete (nothing changed).</returns>
    public bool Reopen(AuditStamp stamp)
    {
        if (CompletedOn is null)
            return false;

        CompletedOn = null;
        Completed = null;
        Touch(stamp);
        return true;
    }

    private void Touch(AuditStamp stamp)
    {
        Updated = stamp;
        Version++;
    }
}
