using Amazon.DynamoDBv2.Model;
using PDS.Documents.Contracts;
using PDS.Documents.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared.Data;

namespace PDS.Documents.Data;

/// <summary>Document item: pk=PROJECT#projectId, sk=DOC#documentId. Optional attributes are omitted when empty.</summary>
internal static class DocumentItemMapper
{
    public const string DocumentPrefix = "DOC#";
    public const string ItemTypeAttr = "itemType";
    public const string ItemType = "Document";

    public static string PartitionKey(ProjectId projectId) => $"PROJECT#{projectId.Value}";

    public static string SortKey(DocumentId documentId) => $"{DocumentPrefix}{documentId.Value}";

    public static Dictionary<string, AttributeValue> Key(ProjectId projectId, DocumentId documentId) => new()
    {
        [TableKeys.PartitionKey] = Attr.S(PartitionKey(projectId)),
        [TableKeys.SortKey] = Attr.S(SortKey(documentId)),
    };

    public static Dictionary<string, AttributeValue> ToItem(Document d)
    {
        var f = d.Fields;
        var item = Key(d.ProjectId, d.Id);
        item[ItemTypeAttr] = Attr.S(ItemType);
        item["id"] = Attr.S(d.Id.Value.ToString());
        item["projectId"] = Attr.S(d.ProjectId.Value.ToString());
        item["title"] = Attr.S(f.Title);
        item["category"] = Attr.S(f.Category);
        if (f.Link is { } link)
        {
            item["link"] = Attr.Map(new Dictionary<string, AttributeValue>
            {
                ["module"] = Attr.S(link.Module!.Value.ToString()),
                ["recordId"] = Attr.S(link.RecordId.ToString()),
                ["label"] = Attr.S(link.Label),
            });
        }

        if (f.Tags.Count > 0)
            item["tags"] = new AttributeValue { L = f.Tags.Select(Attr.S).ToList() };
        Attr.SetIfPresent(item, "notes", f.Notes);
        VersionItemMapper.WriteVersion(item, d.CurrentVersion);
        if (d.Deleted is { } deleted)
        {
            item["deletedAt"] = Attr.Timestamp(deleted.At);
            item["deletedBy"] = Attr.S(deleted.By);
        }

        item["createdAt"] = Attr.Timestamp(d.Created.At);
        item["createdBy"] = Attr.S(d.Created.By);
        item["updatedAt"] = Attr.Timestamp(d.Updated.At);
        item["updatedBy"] = Attr.S(d.Updated.By);
        item["version"] = Attr.N(d.Version);
        return item;
    }

    public static Document FromItem(IReadOnlyDictionary<string, AttributeValue> item)
    {
        DocumentLink? link = null;
        if (item.TryGetValue("link", out var linkValue) && linkValue.M is { } m)
        {
            link = new DocumentLink(
                Enum.Parse<LinkedModule>(Attr.GetString(m, "module")),
                Guid.Parse(Attr.GetString(m, "recordId")),
                Attr.GetString(m, "label"));
        }

        IReadOnlyList<string> tags = item.TryGetValue("tags", out var tagsValue) && tagsValue.L is { } list
            ? list.Select(v => v.S).Where(s => s is not null).ToList()!
            : [];

        var fields = new DocumentFields(
            Attr.GetString(item, "title"), Attr.GetString(item, "category"), link, tags, Attr.GetStringOrNull(item, "notes"));

        AuditStamp? deleted = Attr.GetStringOrNull(item, "deletedAt") is not null
            ? new AuditStamp(Attr.GetTimestamp(item, "deletedAt"), Attr.GetString(item, "deletedBy"))
            : null;

        return Document.Rehydrate(
            new DocumentId(Guid.Parse(Attr.GetString(item, "id"))),
            new ProjectId(Guid.Parse(Attr.GetString(item, "projectId"))),
            fields,
            VersionItemMapper.ReadVersion(item),
            deleted,
            new AuditStamp(Attr.GetTimestamp(item, "createdAt"), Attr.GetString(item, "createdBy")),
            new AuditStamp(Attr.GetTimestamp(item, "updatedAt"), Attr.GetString(item, "updatedBy")),
            Attr.GetLong(item, "version"));
    }
}
