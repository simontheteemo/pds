namespace PDS.Portfolio.Contracts;

public readonly record struct ProjectId(Guid Value)
{
    public static ProjectId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
