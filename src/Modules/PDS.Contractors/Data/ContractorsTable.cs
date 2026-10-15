using PDS.Shared.Data;

namespace PDS.Contractors.Data;

internal static class ContractorsTable
{
    public const string LogicalName = "contractors";
    public const string Gsi1 = "gsi1";

    public static readonly TableDefinition Definition = new(LogicalName, [new GlobalIndex(Gsi1, "gsi1pk", "gsi1sk")]);
}
