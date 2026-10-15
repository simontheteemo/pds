using PDS.Shared.Data;

namespace PDS.Sales.Data;

internal static class SalesTable
{
    public const string LogicalName = "sales";
    public const string Gsi1 = "gsi1";

    public static readonly TableDefinition Definition = new(LogicalName, [new GlobalIndex(Gsi1, "gsi1pk", "gsi1sk")]);
}
