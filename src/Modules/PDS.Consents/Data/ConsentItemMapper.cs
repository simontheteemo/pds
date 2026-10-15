using Amazon.DynamoDBv2.Model;
using PDS.Consents.Contracts;
using PDS.Consents.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared.Data;

namespace PDS.Consents.Data;

/// <summary>Consent item: pk=PROJECT#projectId, sk=CONSENT#consentId. Optional attributes are omitted when empty.</summary>
internal static class ConsentItemMapper
{
    public const string ConsentPrefix = "CONSENT#";
    public const string ItemTypeAttr = "itemType";
    public const string ItemType = "Consent";

    public static string PartitionKey(ProjectId projectId) => $"PROJECT#{projectId.Value}";

    public static string SortKey(ConsentId consentId) => $"{ConsentPrefix}{consentId.Value}";

    public static Dictionary<string, AttributeValue> Key(ProjectId projectId, ConsentId consentId) => new()
    {
        [TableKeys.PartitionKey] = Attr.S(PartitionKey(projectId)),
        [TableKeys.SortKey] = Attr.S(SortKey(consentId)),
    };

    public static Dictionary<string, AttributeValue> ToItem(Consent consent)
    {
        var f = consent.Fields;
        var item = Key(consent.ProjectId, consent.Id);
        item[ItemTypeAttr] = Attr.S(ItemType);
        item["id"] = Attr.S(consent.Id.Value.ToString());
        item["projectId"] = Attr.S(consent.ProjectId.Value.ToString());
        item["type"] = Attr.S(f.Type.ToString());
        item["title"] = Attr.S(f.Title);
        Attr.SetIfPresent(item, "authority", f.Authority);
        Attr.SetIfPresent(item, "reference", f.Reference);
        item["status"] = Attr.S(f.Status.ToString());
        Attr.SetIfPresent(item, "lodgedDate", f.LodgedDate);
        Attr.SetIfPresent(item, "grantedDate", f.GrantedDate);
        Attr.SetIfPresent(item, "expiryDate", f.ExpiryDate);
        Attr.SetIfPresent(item, "notes", f.Notes);
        item["createdAt"] = Attr.Timestamp(consent.Created.At);
        item["createdBy"] = Attr.S(consent.Created.By);
        item["updatedAt"] = Attr.Timestamp(consent.Updated.At);
        item["updatedBy"] = Attr.S(consent.Updated.By);
        item["version"] = Attr.N(consent.Version);
        return item;
    }

    public static Consent FromItem(IReadOnlyDictionary<string, AttributeValue> item)
    {
        var fields = new ConsentFields(
            Type: Enum.Parse<ConsentType>(Attr.GetString(item, "type")),
            Title: Attr.GetString(item, "title"),
            Authority: Attr.GetStringOrNull(item, "authority"),
            Reference: Attr.GetStringOrNull(item, "reference"),
            Status: Enum.Parse<ConsentStatus>(Attr.GetString(item, "status")),
            LodgedDate: Attr.GetDateOrNull(item, "lodgedDate"),
            GrantedDate: Attr.GetDateOrNull(item, "grantedDate"),
            ExpiryDate: Attr.GetDateOrNull(item, "expiryDate"),
            Notes: Attr.GetStringOrNull(item, "notes"));

        return Consent.Rehydrate(
            new ConsentId(Guid.Parse(Attr.GetString(item, "id"))),
            new ProjectId(Guid.Parse(Attr.GetString(item, "projectId"))),
            fields,
            new AuditStamp(Attr.GetTimestamp(item, "createdAt"), Attr.GetString(item, "createdBy")),
            new AuditStamp(Attr.GetTimestamp(item, "updatedAt"), Attr.GetString(item, "updatedBy")),
            Attr.GetLong(item, "version"));
    }
}
