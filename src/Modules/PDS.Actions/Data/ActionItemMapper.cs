using System.Globalization;
using Amazon.DynamoDBv2.Model;
using PDS.Actions.Contracts;
using PDS.Actions.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared.Data;

namespace PDS.Actions.Data;

/// <summary>
/// Action item: pk=PROJECT#projectId, sk=ACTION#actionId. Optional attributes are omitted when empty.
/// The gsi attributes are written only while the action is open, so a full Put of a closed action drops it from both indexes.
/// </summary>
internal static class ActionItemMapper
{
    public const string ActionPrefix = "ACTION#";
    public const string OpenPartition = "OPEN";
    private const string DateFormat = "yyyy-MM-dd";

    public static string PartitionKey(ProjectId projectId) => $"PROJECT#{projectId.Value}";

    public static string OwnerPartition(string ownerId) => $"OWNER#{ownerId}";

    public static string SortableDate(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);

    public static Dictionary<string, AttributeValue> Key(ProjectId projectId, ActionId actionId) => new()
    {
        [TableKeys.PartitionKey] = Attr.S(PartitionKey(projectId)),
        [TableKeys.SortKey] = Attr.S($"{ActionPrefix}{actionId.Value}"),
    };

    public static Dictionary<string, AttributeValue> ToItem(ActionItem a)
    {
        var f = a.Fields;
        var item = Key(a.ProjectId, a.Id);
        item["id"] = Attr.S(a.Id.Value.ToString());
        item["projectId"] = Attr.S(a.ProjectId.Value.ToString());
        item["title"] = Attr.S(f.Title);
        Attr.SetIfPresent(item, "description", f.Description);
        item["ownerId"] = Attr.S(f.OwnerId);
        item["ownerName"] = Attr.S(f.OwnerName);
        Attr.SetIfPresent(item, "dueDate", f.DueDate);
        item["priority"] = Attr.S(f.Priority.ToString());
        item["status"] = Attr.S(a.Status.ToString());
        if (f.Link is { } link)
        {
            item["link"] = Attr.Map(new Dictionary<string, AttributeValue>
            {
                ["module"] = Attr.S(link.Module!.Value.ToString()),
                ["recordId"] = Attr.S(link.RecordId.ToString()),
                ["label"] = Attr.S(link.Label),
            });
        }

        if (a.Completed is { } completed)
        {
            item["completedAt"] = Attr.Timestamp(completed.At);
            item["completedBy"] = Attr.S(completed.By);
        }

        item["createdAt"] = Attr.Timestamp(a.Created.At);
        item["createdBy"] = Attr.S(a.Created.By);
        item["updatedAt"] = Attr.Timestamp(a.Updated.At);
        item["updatedBy"] = Attr.S(a.Updated.By);
        item["version"] = Attr.N(a.Version);

        if (a.IsOpen)
        {
            var dueKey = f.DueDate is { } d ? SortableDate(d) : ActionRules.NoDueSortKey;
            item["gsi1pk"] = Attr.S(OwnerPartition(f.OwnerId));
            item["gsi1sk"] = Attr.S($"{dueKey}#{a.Id.Value}");
            if (f.DueDate is { } due)
            {
                item["gsi2pk"] = Attr.S(OpenPartition);
                item["gsi2sk"] = Attr.S($"{SortableDate(due)}#{a.Id.Value}");
            }
        }

        return item;
    }

    public static ActionItem FromItem(IReadOnlyDictionary<string, AttributeValue> item)
    {
        ActionLink? link = null;
        if (item.TryGetValue("link", out var linkValue) && linkValue.M is { } m)
        {
            link = new ActionLink(
                Enum.Parse<ActionLinkedModule>(Attr.GetString(m, "module")),
                Guid.Parse(Attr.GetString(m, "recordId")),
                Attr.GetString(m, "label"));
        }

        var fields = new ActionFields(
            Title: Attr.GetString(item, "title"),
            Description: Attr.GetStringOrNull(item, "description"),
            OwnerId: Attr.GetString(item, "ownerId"),
            OwnerName: Attr.GetString(item, "ownerName"),
            DueDate: Attr.GetDateOrNull(item, "dueDate"),
            Priority: Enum.Parse<ActionPriority>(Attr.GetString(item, "priority")),
            Link: link);

        AuditStamp? completed = Attr.GetStringOrNull(item, "completedAt") is not null
            ? new AuditStamp(Attr.GetTimestamp(item, "completedAt"), Attr.GetString(item, "completedBy"))
            : null;

        return ActionItem.Rehydrate(
            new ActionId(Guid.Parse(Attr.GetString(item, "id"))),
            new ProjectId(Guid.Parse(Attr.GetString(item, "projectId"))),
            fields,
            Enum.Parse<ActionStatus>(Attr.GetString(item, "status")),
            completed,
            new AuditStamp(Attr.GetTimestamp(item, "createdAt"), Attr.GetString(item, "createdBy")),
            new AuditStamp(Attr.GetTimestamp(item, "updatedAt"), Attr.GetString(item, "updatedBy")),
            Attr.GetLong(item, "version"));
    }
}
