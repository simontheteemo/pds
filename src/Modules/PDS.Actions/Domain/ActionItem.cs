using PDS.Actions.Contracts;
using PDS.Portfolio.Contracts;

namespace PDS.Actions.Domain;

/// <summary>A follow-up task on a project. Named ActionItem to avoid clashing with System.Action.</summary>
internal sealed class ActionItem
{
    private ActionItem(
        ActionId id, ProjectId projectId, ActionFields fields, ActionStatus status, AuditStamp? completed,
        AuditStamp created, AuditStamp updated, long version)
    {
        Id = id;
        ProjectId = projectId;
        Fields = fields;
        Status = status;
        Completed = completed;
        Created = created;
        Updated = updated;
        Version = version;
    }

    public ActionId Id { get; }

    public ProjectId ProjectId { get; }

    public ActionFields Fields { get; private set; }

    public ActionStatus Status { get; private set; }

    /// <summary>When and by whom the action was completed or cancelled; null while open.</summary>
    public AuditStamp? Completed { get; private set; }

    public AuditStamp Created { get; }

    public AuditStamp Updated { get; private set; }

    public long Version { get; private set; }

    public bool IsOpen => Status is ActionStatus.Open or ActionStatus.InProgress;

    public bool IsOverdue(DateOnly today) => IsOpen && Fields.DueDate is { } due && due < today;

    public static ActionItem Create(ProjectId projectId, ActionFields fields, AuditStamp stamp)
    {
        var normalised = ActionRules.Normalise(fields);
        ActionRules.EnsureValid(normalised);
        return new ActionItem(ActionId.New(), projectId, normalised, ActionStatus.Open, null, stamp, stamp, 1);
    }

    public static ActionItem Rehydrate(
        ActionId id, ProjectId projectId, ActionFields fields, ActionStatus status, AuditStamp? completed,
        AuditStamp created, AuditStamp updated, long version) =>
        new(id, projectId, fields, status, completed, created, updated, version);

    /// <summary>Edits fields and moves between Open and InProgress. Closed actions must be reopened first.</summary>
    public void Update(ActionFields fields, ActionStatus status, AuditStamp stamp)
    {
        if (!IsOpen)
            throw new InvalidOperationException("A closed action must be reopened before it is edited.");
        if (status is ActionStatus.Done or ActionStatus.Cancelled)
            throw new ArgumentException("Use Complete or Cancel to close an action.", nameof(status));

        var normalised = ActionRules.Normalise(fields);
        ActionRules.EnsureValid(normalised);
        Fields = normalised;
        Status = status;
        Touch(stamp);
    }

    /// <returns>False when the action was already closed (nothing changed).</returns>
    public bool Complete(AuditStamp stamp) => Close(ActionStatus.Done, stamp);

    /// <returns>False when the action was already closed (nothing changed).</returns>
    public bool Cancel(AuditStamp stamp) => Close(ActionStatus.Cancelled, stamp);

    /// <returns>False when the action was already open (nothing changed).</returns>
    public bool Reopen(AuditStamp stamp)
    {
        if (IsOpen)
            return false;

        Status = ActionStatus.Open;
        Completed = null;
        Touch(stamp);
        return true;
    }

    private bool Close(ActionStatus status, AuditStamp stamp)
    {
        if (!IsOpen)
            return false;

        Status = status;
        Completed = stamp;
        Touch(stamp);
        return true;
    }

    private void Touch(AuditStamp stamp)
    {
        Updated = stamp;
        Version++;
    }
}
