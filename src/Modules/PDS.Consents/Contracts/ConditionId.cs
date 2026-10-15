namespace PDS.Consents.Contracts;

public readonly record struct ConditionId(Guid Value)
{
    public static ConditionId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
