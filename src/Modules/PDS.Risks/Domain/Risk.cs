using PDS.Portfolio.Contracts;
using PDS.Risks.Contracts;

namespace PDS.Risks.Domain;

internal sealed class Risk
{
    private Risk(
        RiskId id, ProjectId projectId, RiskFields fields, RiskStatus status, string? closingNote,
        AuditStamp? closed, AuditStamp created, AuditStamp updated, long version)
    {
        Id = id;
        ProjectId = projectId;
        Fields = fields;
        Status = status;
        ClosingNote = closingNote;
        Closed = closed;
        Created = created;
        Updated = updated;
        Version = version;
    }

    public RiskId Id { get; }

    public ProjectId ProjectId { get; }

    public RiskFields Fields { get; private set; }

    public RiskStatus Status { get; private set; }

    public string? ClosingNote { get; private set; }

    public AuditStamp? Closed { get; private set; }

    public AuditStamp Created { get; }

    public AuditStamp Updated { get; private set; }

    public long Version { get; private set; }

    public int Score => RiskRules.Score(Fields.Likelihood, Fields.Impact);

    public RiskBand Band => RiskRules.BandFor(Score);

    public static Risk Create(ProjectId projectId, RiskFields fields, AuditStamp stamp)
    {
        var normalised = RiskRules.Normalise(fields);
        RiskRules.EnsureValid(normalised);
        return new Risk(RiskId.New(), projectId, normalised, RiskStatus.Open, null, null, stamp, stamp, 1);
    }

    public static Risk Rehydrate(
        RiskId id, ProjectId projectId, RiskFields fields, RiskStatus status, string? closingNote,
        AuditStamp? closed, AuditStamp created, AuditStamp updated, long version) =>
        new(id, projectId, fields, status, closingNote, closed, created, updated, version);

    /// <summary>Edits fields and moves between Open and Mitigating. Closed risks must be reopened first.</summary>
    public void Update(RiskFields fields, RiskStatus status, AuditStamp stamp)
    {
        if (Status == RiskStatus.Closed)
            throw new InvalidOperationException("A closed risk must be reopened before it is edited.");
        if (status == RiskStatus.Closed)
            throw new ArgumentException("Use Close to close a risk.", nameof(status));

        var normalised = RiskRules.Normalise(fields);
        RiskRules.EnsureValid(normalised);
        Fields = normalised;
        Status = status;
        Touch(stamp);
    }

    /// <returns>False when the risk was already closed (nothing changed).</returns>
    public bool Close(string note, AuditStamp stamp)
    {
        if (Status == RiskStatus.Closed)
            return false;
        var trimmed = (note ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new ArgumentException("A closing note is required.", nameof(note));

        Status = RiskStatus.Closed;
        ClosingNote = trimmed;
        Closed = stamp;
        Touch(stamp);
        return true;
    }

    /// <returns>False when the risk was not closed (nothing changed).</returns>
    public bool Reopen(AuditStamp stamp)
    {
        if (Status != RiskStatus.Closed)
            return false;

        Status = RiskStatus.Open;
        ClosingNote = null;
        Closed = null;
        Touch(stamp);
        return true;
    }

    private void Touch(AuditStamp stamp)
    {
        Updated = stamp;
        Version++;
    }
}
