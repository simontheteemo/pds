using PDS.Documents.Contracts;
using PDS.Portfolio.Contracts;

namespace PDS.Documents.Domain;

internal sealed class Document
{
    private Document(
        DocumentId id, ProjectId projectId, DocumentFields fields, DocumentVersion current, AuditStamp? deleted,
        AuditStamp created, AuditStamp updated, long version)
    {
        Id = id;
        ProjectId = projectId;
        Fields = fields;
        CurrentVersion = current;
        Deleted = deleted;
        Created = created;
        Updated = updated;
        Version = version;
    }

    public DocumentId Id { get; }

    public ProjectId ProjectId { get; }

    public DocumentFields Fields { get; private set; }

    public DocumentVersion CurrentVersion { get; private set; }

    public AuditStamp? Deleted { get; private set; }

    public bool IsDeleted => Deleted is not null;

    public AuditStamp Created { get; }

    public AuditStamp Updated { get; private set; }

    public long Version { get; private set; }

    /// <summary>Fields must already be validated and normalised.</summary>
    public static Document Create(DocumentId id, ProjectId projectId, DocumentFields fields, DocumentVersion first, AuditStamp stamp) =>
        first.Number == 1
            ? new Document(id, projectId, fields, first, null, stamp, stamp, 1)
            : throw new ArgumentException("The first version must be number 1.", nameof(first));

    public static Document Rehydrate(
        DocumentId id, ProjectId projectId, DocumentFields fields, DocumentVersion current, AuditStamp? deleted,
        AuditStamp created, AuditStamp updated, long version) =>
        new(id, projectId, fields, current, deleted, created, updated, version);

    public void Update(DocumentFields fields, AuditStamp stamp)
    {
        Fields = fields;
        Touch(stamp);
    }

    public void AddVersion(DocumentVersion next, AuditStamp stamp)
    {
        if (next.Number != CurrentVersion.Number + 1)
            throw new ArgumentException("Versions are numbered consecutively.", nameof(next));
        CurrentVersion = next;
        Touch(stamp);
    }

    public void MarkDeleted(AuditStamp stamp)
    {
        Deleted = stamp;
        Touch(stamp);
    }

    private void Touch(AuditStamp stamp)
    {
        Updated = stamp;
        Version++;
    }
}
