using Amazon.DynamoDBv2.Model;
using PDS.Contractors.Contracts;
using PDS.Portfolio.Contracts;
using PDS.Shared.Data;

namespace PDS.Costs.Data;

/// <summary>All cost items for a project share pk=PROJECT#projectId; the sk prefix selects the item type.</summary>
internal static class CostKeys
{
    public const string LinePrefix = "LINE#";
    public const string CommitmentPrefix = "COMMIT#";
    public const string ActualPrefix = "ACTUAL#";

    public static string Pk(ProjectId p) => $"PROJECT#{p}";

    /// <summary>gsi1 partition key for actuals linked to a contractor.</summary>
    public static string ContractorKey(ContractorId id) => $"CONTRACTOR#{id.Value}";

    public static Dictionary<string, AttributeValue> Key(ProjectId p, string sk) =>
        new() { [TableKeys.PartitionKey] = Attr.S(Pk(p)), [TableKeys.SortKey] = Attr.S(sk) };
}
