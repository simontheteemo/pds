namespace PDS.Sales.Contracts;

public readonly record struct UnitId(Guid Value)
{
    public static UnitId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
