using System.Globalization;
using Amazon.DynamoDBv2.Model;
using PDS.Documents.Contracts;
using PDS.Documents.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared.Data;

namespace PDS.Documents.Data;

/// <summary>Version item: pk=PROJECT#projectId, sk=DOC#documentId#V#000001 (sorts under its document).</summary>
internal static class VersionItemMapper
{
    public const string ItemType = "Version";

    public static string SortKey(DocumentId documentId, int number) =>
        $"{DocumentItemMapper.SortKey(documentId)}#V#{number.ToString("D6", CultureInfo.InvariantCulture)}";

    public static Dictionary<string, AttributeValue> Key(ProjectId projectId, DocumentId documentId, int number) => new()
    {
        [TableKeys.PartitionKey] = Attr.S(DocumentItemMapper.PartitionKey(projectId)),
        [TableKeys.SortKey] = Attr.S(SortKey(documentId, number)),
    };

    public static Dictionary<string, AttributeValue> ToItem(ProjectId projectId, DocumentId documentId, DocumentVersion v)
    {
        var item = Key(projectId, documentId, v.Number);
        item[DocumentItemMapper.ItemTypeAttr] = Attr.S(ItemType);
        item["documentId"] = Attr.S(documentId.Value.ToString());
        WriteVersion(item, v);
        return item;
    }

    /// <summary>Shared by the version item and the document item's inline current version.</summary>
    public static void WriteVersion(Dictionary<string, AttributeValue> item, DocumentVersion v)
    {
        item["versionNumber"] = Attr.N(v.Number);
        item["fileName"] = Attr.S(v.FileName);
        item["contentType"] = Attr.S(v.ContentType);
        item["sizeBytes"] = Attr.N(v.SizeBytes);
        item["storageKey"] = Attr.S(v.StorageKey);
        item["uploadedAt"] = Attr.Timestamp(v.Uploaded.At);
        item["uploadedBy"] = Attr.S(v.Uploaded.By);
    }

    public static DocumentVersion ReadVersion(IReadOnlyDictionary<string, AttributeValue> item) => new(
        (int)Attr.GetLong(item, "versionNumber"),
        Attr.GetString(item, "fileName"),
        Attr.GetString(item, "contentType"),
        Attr.GetLong(item, "sizeBytes"),
        Attr.GetString(item, "storageKey"),
        new AuditStamp(Attr.GetTimestamp(item, "uploadedAt"), Attr.GetString(item, "uploadedBy")));

    public static DocumentId DocumentIdOf(IReadOnlyDictionary<string, AttributeValue> item) =>
        new(Guid.Parse(Attr.GetString(item, "documentId")));
}
