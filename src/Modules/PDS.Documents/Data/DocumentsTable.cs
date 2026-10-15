using PDS.Shared.Data;

namespace PDS.Documents.Data;

internal static class DocumentsTable
{
    public const string LogicalName = "documents";

    public static readonly TableDefinition Definition = new(LogicalName, []);
}
