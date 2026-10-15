using PDS.Shared.Data;

namespace PDS.Costs.Data;

internal static class CostsTable
{
    public const string LogicalName = "costs";

    public static readonly TableDefinition Definition = new(LogicalName, []);
}
