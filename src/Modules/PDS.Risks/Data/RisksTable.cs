using PDS.Shared.Data;

namespace PDS.Risks.Data;

internal static class RisksTable
{
    public const string LogicalName = "risks";

    public static readonly TableDefinition Definition = new(LogicalName, []);
}
