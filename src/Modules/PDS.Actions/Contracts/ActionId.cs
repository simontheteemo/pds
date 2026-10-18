namespace PDS.Actions.Contracts;

public readonly record struct ActionId(Guid Value)
{
    public static ActionId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
