namespace PDS.Contractors.Contracts;

public readonly record struct ContractId(Guid Value)
{
    public static ContractId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
