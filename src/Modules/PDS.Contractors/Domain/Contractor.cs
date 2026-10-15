using PDS.Contractors.Contracts;

namespace PDS.Contractors.Domain;

internal sealed class Contractor
{
    private Contractor(
        ContractorId id, ContractorFields fields, bool isActive, AuditStamp created, AuditStamp updated, long version)
    {
        Id = id;
        Fields = fields;
        IsActive = isActive;
        Created = created;
        Updated = updated;
        Version = version;
    }

    public ContractorId Id { get; }

    public ContractorFields Fields { get; private set; }

    public bool IsActive { get; private set; }

    public AuditStamp Created { get; }

    public AuditStamp Updated { get; private set; }

    public long Version { get; private set; }

    public static Contractor Create(ContractorFields fields, AuditStamp stamp)
    {
        var normalised = ContractorRules.Normalise(fields);
        ContractorRules.EnsureValid(normalised);
        return new Contractor(ContractorId.New(), normalised, true, stamp, stamp, 1);
    }

    public static Contractor Rehydrate(
        ContractorId id, ContractorFields fields, bool isActive, AuditStamp created, AuditStamp updated, long version) =>
        new(id, fields, isActive, created, updated, version);

    public void Update(ContractorFields fields, AuditStamp stamp)
    {
        var normalised = ContractorRules.Normalise(fields);
        ContractorRules.EnsureValid(normalised);
        Fields = normalised;
        Touch(stamp);
    }

    /// <returns>False when the contractor was already in the requested state (nothing changed).</returns>
    public bool SetActive(bool active, AuditStamp stamp)
    {
        if (IsActive == active)
            return false;
        IsActive = active;
        Touch(stamp);
        return true;
    }

    public ComplianceStatus ComplianceOn(DateOnly today) => Compliance.Overall(Fields.Expiries, today);

    private void Touch(AuditStamp stamp)
    {
        Updated = stamp;
        Version++;
    }
}
