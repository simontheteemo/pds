namespace PDS.Costs.Contracts;

public readonly record struct BudgetLineId(Guid Value)
{
    public static BudgetLineId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
