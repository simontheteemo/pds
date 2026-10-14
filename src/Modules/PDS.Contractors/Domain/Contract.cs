using PDS.Contractors.Contracts;
using PDS.Portfolio.Contracts;

namespace PDS.Contractors.Domain;

internal sealed class Contract
{
    private Contract(
        ContractId id, ProjectId projectId, ContractorId contractorId, ContractFields fields,
        AuditStamp created, AuditStamp updated, long version)
    {
        Id = id;
        ProjectId = projectId;
        ContractorId = contractorId;
        Fields = fields;
        Created = created;
        Updated = updated;
        Version = version;
    }

    public ContractId Id { get; }

    public ProjectId ProjectId { get; }

    /// <summary>Fixed at creation.</summary>
    public ContractorId ContractorId { get; }

    public ContractFields Fields { get; private set; }

    public AuditStamp Created { get; }

    public AuditStamp Updated { get; private set; }

    public long Version { get; private set; }

    public static Contract Create(ProjectId projectId, ContractorId contractorId, ContractFields fields, AuditStamp stamp)
    {
        var normalised = ContractRules.Normalise(fields);
        ContractRules.EnsureValid(normalised);
        return new Contract(ContractId.New(), projectId, contractorId, normalised, stamp, stamp, 1);
    }

    public static Contract Rehydrate(
        ContractId id, ProjectId projectId, ContractorId contractorId, ContractFields fields,
        AuditStamp created, AuditStamp updated, long version) =>
        new(id, projectId, contractorId, fields, created, updated, version);

    public void Update(ContractFields fields, AuditStamp stamp)
    {
        var normalised = ContractRules.Normalise(fields);
        ContractRules.EnsureValid(normalised);
        Fields = normalised;
        Updated = stamp;
        Version++;
    }
}
