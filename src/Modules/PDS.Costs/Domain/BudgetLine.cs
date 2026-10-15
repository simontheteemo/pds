using PDS.Costs.Contracts;
using PDS.Portfolio.Contracts;

namespace PDS.Costs.Domain;

internal sealed class BudgetLine
{
    private BudgetLine(
        BudgetLineId id, ProjectId projectId, BudgetLineFields fields, AuditStamp created, AuditStamp updated, long version)
    {
        Id = id;
        ProjectId = projectId;
        Fields = fields;
        Created = created;
        Updated = updated;
        Version = version;
    }

    public BudgetLineId Id { get; }

    public ProjectId ProjectId { get; }

    public BudgetLineFields Fields { get; private set; }

    public AuditStamp Created { get; }

    public AuditStamp Updated { get; private set; }

    public long Version { get; private set; }

    public static BudgetLine Create(ProjectId projectId, BudgetLineFields fields, AuditStamp stamp)
    {
        var normalised = BudgetLineRules.Normalise(fields);
        BudgetLineRules.EnsureValid(normalised);
        return new BudgetLine(BudgetLineId.New(), projectId, normalised, stamp, stamp, 1);
    }

    public static BudgetLine Rehydrate(
        BudgetLineId id, ProjectId projectId, BudgetLineFields fields, AuditStamp created, AuditStamp updated, long version) =>
        new(id, projectId, fields, created, updated, version);

    public void Update(BudgetLineFields fields, AuditStamp stamp)
    {
        var normalised = BudgetLineRules.Normalise(fields);
        BudgetLineRules.EnsureValid(normalised);
        Fields = normalised;
        Updated = stamp;
        Version++;
    }
}
