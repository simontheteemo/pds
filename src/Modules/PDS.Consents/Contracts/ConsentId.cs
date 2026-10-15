namespace PDS.Consents.Contracts;

public readonly record struct ConsentId(Guid Value)
{
    public static ConsentId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
