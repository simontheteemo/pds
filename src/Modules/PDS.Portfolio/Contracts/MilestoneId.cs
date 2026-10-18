namespace PDS.Portfolio.Contracts;

public readonly record struct MilestoneId(Guid Value)
{
    public static MilestoneId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
