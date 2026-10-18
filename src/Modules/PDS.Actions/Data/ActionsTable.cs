using PDS.Shared.Data;

namespace PDS.Actions.Data;

/// <summary>
/// Actions table. gsi1 (gsi1pk=OWNER#ownerId, gsi1sk=due-or-9999-12-31#actionId) lists open actions per owner;
/// gsi2 (gsi2pk=OPEN, gsi2sk=due#actionId) lists open actions with a due date. Both are sparse: closed actions omit them.
/// </summary>
internal static class ActionsTable
{
    public const string LogicalName = "actions";
    public const string Gsi1 = "gsi1";
    public const string Gsi2 = "gsi2";

    public static readonly TableDefinition Definition = new(
        LogicalName,
        [new GlobalIndex(Gsi1, "gsi1pk", "gsi1sk"), new GlobalIndex(Gsi2, "gsi2pk", "gsi2sk")]);
}
