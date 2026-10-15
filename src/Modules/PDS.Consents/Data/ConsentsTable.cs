using PDS.Shared.Data;

namespace PDS.Consents.Data;

internal static class ConsentsTable
{
    public const string LogicalName = "consents";

    public static readonly TableDefinition Definition = new(LogicalName, []);
}
