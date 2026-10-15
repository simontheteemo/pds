namespace PDS.Contractors.Contracts;

public readonly record struct ContractorId(Guid Value)
{
    public static ContractorId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
