using Amazon.DynamoDBv2.Model;
using PDS.Documents.Contracts;
using PDS.Portfolio.Contracts;
using PDS.Shared.Data;

namespace PDS.Documents.Data;

/// <summary>
/// Upload claim: pk=PROJECT#projectId, sk=UPLOAD#uploadId → the document created from that upload.
/// Makes confirming the same upload idempotent. Never matches the DOC# queries.
/// </summary>
internal static class UploadClaimItemMapper
{
    public const string ItemType = "UploadClaim";
    private const string DocumentIdAttr = "documentId";

    public static Dictionary<string, AttributeValue> Key(ProjectId projectId, Guid uploadId) => new()
    {
        [TableKeys.PartitionKey] = Attr.S(DocumentItemMapper.PartitionKey(projectId)),
        [TableKeys.SortKey] = Attr.S($"UPLOAD#{uploadId}"),
    };

    public static Dictionary<string, AttributeValue> ToItem(ProjectId projectId, Guid uploadId, DocumentId documentId)
    {
        var item = Key(projectId, uploadId);
        item[DocumentItemMapper.ItemTypeAttr] = Attr.S(ItemType);
        item[DocumentIdAttr] = Attr.S(documentId.Value.ToString());
        return item;
    }

    public static DocumentId DocumentIdOf(IReadOnlyDictionary<string, AttributeValue> item) =>
        new(Guid.Parse(Attr.GetString(item, DocumentIdAttr)));
}
