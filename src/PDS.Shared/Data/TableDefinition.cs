namespace PDS.Shared.Data;

public static class TableKeys
{
    public const string PartitionKey = "pk";
    public const string SortKey = "sk";
}

public sealed record GlobalIndex(string Name, string PartitionKey, string SortKey);

/// <summary>Key layout of one module table. Local dev and tests create tables from it; CDK creates the real ones.</summary>
public sealed record TableDefinition(string LogicalName, IReadOnlyList<GlobalIndex> GlobalIndexes);
