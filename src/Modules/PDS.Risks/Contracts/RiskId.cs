namespace PDS.Risks.Contracts;

public readonly record struct RiskId(Guid Value)
{
    public static RiskId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
