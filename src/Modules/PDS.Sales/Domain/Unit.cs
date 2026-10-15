using PDS.Portfolio.Contracts;
using PDS.Sales.Contracts;
using PDS.Shared;

namespace PDS.Sales.Domain;

internal sealed class Unit
{
    private static readonly SaleDetails EmptySale = new(null, null, null, null, null, null, null, null, null);

    private Unit(
        UnitId id, ProjectId projectId, UnitFields fields, SaleStatus status, SaleDetails? sale,
        AuditStamp created, AuditStamp updated, long version)
    {
        Id = id;
        ProjectId = projectId;
        Fields = fields;
        Status = status;
        Sale = sale;
        Created = created;
        Updated = updated;
        Version = version;
    }

    public UnitId Id { get; }

    public ProjectId ProjectId { get; }

    public UnitFields Fields { get; private set; }

    public SaleStatus Status { get; private set; }

    /// <summary>Null when the status carries no sale (Available, Withdrawn).</summary>
    public SaleDetails? Sale { get; private set; }

    public AuditStamp Created { get; }

    public AuditStamp Updated { get; private set; }

    public long Version { get; private set; }

    /// <summary>The currency shared by every amount on the unit, or null when it has no amounts.</summary>
    public string? Currency => Amounts().FirstOrDefault()?.Currency;

    public static Unit Create(ProjectId projectId, UnitFields fields, AuditStamp stamp)
    {
        var normalised = UnitRules.Normalise(fields);
        UnitRules.EnsureValid(normalised);
        return new Unit(UnitId.New(), projectId, normalised, SaleStatus.Available, null, stamp, stamp, 1);
    }

    public static Unit Rehydrate(
        UnitId id, ProjectId projectId, UnitFields fields, SaleStatus status, SaleDetails? sale,
        AuditStamp created, AuditStamp updated, long version) =>
        new(id, projectId, fields, status, sale, created, updated, version);

    public void Update(UnitFields fields, AuditStamp stamp)
    {
        var normalised = UnitRules.Normalise(fields);
        UnitRules.EnsureValid(normalised);
        Fields = normalised;
        EnsureOneCurrency();
        Touch(stamp);
    }

    /// <summary>Moves the sale to <paramref name="status"/>. Available and Withdrawn clear the sale details.</summary>
    public void UpdateSale(SaleStatus status, SaleDetails? sale, AuditStamp stamp)
    {
        SaleDetails? next = null;
        if (SaleRules.HasSale(status))
        {
            next = SaleRules.Normalise(sale ?? EmptySale);
            SaleRules.EnsureValid(status, next);
        }

        Status = status;
        Sale = next;
        EnsureOneCurrency();
        Touch(stamp);
    }

    private IEnumerable<Money> Amounts()
    {
        if (Fields.ListPrice is { } list)
            yield return list;
        if (Sale?.SalePrice is { } price)
            yield return price;
        if (Sale?.Deposit is { } deposit)
            yield return deposit;
    }

    private void EnsureOneCurrency()
    {
        if (Amounts().Select(m => m.Currency).Distinct(StringComparer.Ordinal).Skip(1).Any())
            throw new InvalidOperationException("Every amount on a unit must use the same currency.");
    }

    private void Touch(AuditStamp stamp)
    {
        Updated = stamp;
        Version++;
    }
}
