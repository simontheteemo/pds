namespace PDS.Costs.Contracts;

public readonly record struct ActualCostId(Guid Value)
{
    public static ActualCostId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
