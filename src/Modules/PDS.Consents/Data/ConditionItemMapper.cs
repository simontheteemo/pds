using Amazon.DynamoDBv2.Model;
using PDS.Consents.Contracts;
using PDS.Consents.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared.Data;

using Condition = PDS.Consents.Domain.Condition;

namespace PDS.Consents.Data;

/// <summary>Condition item: pk=PROJECT#projectId, sk=CONSENT#consentId#COND#conditionId (sorts under its consent).</summary>
internal static class ConditionItemMapper
{
    public const string ItemType = "Condition";

    public static string SortKey(ConsentId consentId, ConditionId conditionId) =>
        $"{ConsentItemMapper.SortKey(consentId)}#COND#{conditionId.Value}";

    public static Dictionary<string, AttributeValue> Key(ProjectId projectId, ConsentId consentId, ConditionId conditionId) => new()
    {
        [TableKeys.PartitionKey] = Attr.S(ConsentItemMapper.PartitionKey(projectId)),
        [TableKeys.SortKey] = Attr.S(SortKey(consentId, conditionId)),
    };

    public static Dictionary<string, AttributeValue> ToItem(Condition condition)
    {
        var f = condition.Fields;
        var item = Key(condition.ProjectId, condition.ConsentId, condition.Id);
        item[ConsentItemMapper.ItemTypeAttr] = Attr.S(ItemType);
        item["id"] = Attr.S(condition.Id.Value.ToString());
        item["projectId"] = Attr.S(condition.ProjectId.Value.ToString());
        item["consentId"] = Attr.S(condition.ConsentId.Value.ToString());
        Attr.SetIfPresent(item, "reference", f.Reference);
        item["description"] = Attr.S(f.Description);
        Attr.SetIfPresent(item, "dueDate", f.DueDate);
        Attr.SetIfPresent(item, "completedOn", condition.CompletedOn);
        if (condition.Completed is { } completed)
        {
            item["completedAt"] = Attr.Timestamp(completed.At);
            item["completedBy"] = Attr.S(completed.By);
        }

        item["createdAt"] = Attr.Timestamp(condition.Created.At);
        item["createdBy"] = Attr.S(condition.Created.By);
        item["updatedAt"] = Attr.Timestamp(condition.Updated.At);
        item["updatedBy"] = Attr.S(condition.Updated.By);
        item["version"] = Attr.N(condition.Version);
        return item;
    }

    public static Condition FromItem(IReadOnlyDictionary<string, AttributeValue> item)
    {
        var fields = new ConditionFields(
            Reference: Attr.GetStringOrNull(item, "reference"),
            Description: Attr.GetString(item, "description"),
            DueDate: Attr.GetDateOrNull(item, "dueDate"));

        AuditStamp? completed = Attr.GetStringOrNull(item, "completedAt") is not null
            ? new AuditStamp(Attr.GetTimestamp(item, "completedAt"), Attr.GetString(item, "completedBy"))
            : null;

        return Condition.Rehydrate(
            new ConditionId(Guid.Parse(Attr.GetString(item, "id"))),
            new ProjectId(Guid.Parse(Attr.GetString(item, "projectId"))),
            new ConsentId(Guid.Parse(Attr.GetString(item, "consentId"))),
            fields,
            Attr.GetDateOrNull(item, "completedOn"),
            completed,
            new AuditStamp(Attr.GetTimestamp(item, "createdAt"), Attr.GetString(item, "createdBy")),
            new AuditStamp(Attr.GetTimestamp(item, "updatedAt"), Attr.GetString(item, "updatedBy")),
            Attr.GetLong(item, "version"));
    }
}
