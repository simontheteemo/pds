namespace PDS.Costs.Contracts;

public readonly record struct CommitmentId(Guid Value)
{
    public static CommitmentId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
