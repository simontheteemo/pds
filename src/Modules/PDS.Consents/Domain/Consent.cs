using PDS.Consents.Contracts;
using PDS.Portfolio.Contracts;

namespace PDS.Consents.Domain;

internal sealed class Consent
{
    private Consent(
        ConsentId id, ProjectId projectId, ConsentFields fields, AuditStamp created, AuditStamp updated, long version)
    {
        Id = id;
        ProjectId = projectId;
        Fields = fields;
        Created = created;
        Updated = updated;
        Version = version;
    }

    public ConsentId Id { get; }

    public ProjectId ProjectId { get; }

    public ConsentFields Fields { get; private set; }

    public AuditStamp Created { get; }

    public AuditStamp Updated { get; private set; }

    public long Version { get; private set; }

    public bool IsActive => ConsentRules.IsActive(Fields.Status);

    public static Consent Create(ProjectId projectId, ConsentFields fields, AuditStamp stamp)
    {
        var normalised = ConsentRules.Normalise(fields);
        ConsentRules.EnsureValid(normalised);
        return new Consent(ConsentId.New(), projectId, normalised, stamp, stamp, 1);
    }

    public static Consent Rehydrate(
        ConsentId id, ProjectId projectId, ConsentFields fields, AuditStamp created, AuditStamp updated, long version) =>
        new(id, projectId, fields, created, updated, version);

    public void Update(ConsentFields fields, AuditStamp stamp)
    {
        var normalised = ConsentRules.Normalise(fields);
        ConsentRules.EnsureValid(normalised);
        Fields = normalised;
        Touch(stamp);
    }

    private void Touch(AuditStamp stamp)
    {
        Updated = stamp;
        Version++;
    }
}
