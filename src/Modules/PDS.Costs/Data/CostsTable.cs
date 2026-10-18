using PDS.Shared.Data;

namespace PDS.Costs.Data;

internal static class CostsTable
{
    public const string LogicalName = "costs";
    public const string Gsi1 = "gsi1";

    /// <summary>gsi1 is sparse: only actuals with a contractor carry gsi1pk/gsi1sk.</summary>
    public static readonly TableDefinition Definition = new(LogicalName, [new GlobalIndex(Gsi1, "gsi1pk", "gsi1sk")]);
}
