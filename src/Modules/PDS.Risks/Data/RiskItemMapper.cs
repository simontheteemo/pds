using Amazon.DynamoDBv2.Model;
using PDS.Portfolio.Contracts;
using PDS.Risks.Contracts;
using PDS.Risks.Domain;
using PDS.Shared.Data;

namespace PDS.Risks.Data;

/// <summary>Risk item: pk=PROJECT#projectId, sk=RISK#riskId. Optional attributes are omitted when empty.</summary>
internal static class RiskItemMapper
{
    public const string RiskPrefix = "RISK#";

    public static string PartitionKey(ProjectId projectId) => $"PROJECT#{projectId.Value}";

    public static Dictionary<string, AttributeValue> Key(ProjectId projectId, RiskId riskId) => new()
    {
        [TableKeys.PartitionKey] = Attr.S(PartitionKey(projectId)),
        [TableKeys.SortKey] = Attr.S($"{RiskPrefix}{riskId.Value}"),
    };

    public static Dictionary<string, AttributeValue> ToItem(Risk risk)
    {
        var f = risk.Fields;
        var item = Key(risk.ProjectId, risk.Id);
        item["id"] = Attr.S(risk.Id.Value.ToString());
        item["projectId"] = Attr.S(risk.ProjectId.Value.ToString());
        item["title"] = Attr.S(f.Title);
        item["category"] = Attr.S(f.Category.ToString());
        Attr.SetIfPresent(item, "description", f.Description);
        item["likelihood"] = Attr.N(f.Likelihood);
        item["impact"] = Attr.N(f.Impact);
        Attr.SetIfPresent(item, "mitigation", f.Mitigation);
        Attr.SetIfPresent(item, "owner", f.Owner);
        Attr.SetIfPresent(item, "dueDate", f.DueDate);
        item["status"] = Attr.S(risk.Status.ToString());
        Attr.SetIfPresent(item, "closingNote", risk.ClosingNote);
        if (risk.Closed is { } closed)
        {
            item["closedAt"] = Attr.Timestamp(closed.At);
            item["closedBy"] = Attr.S(closed.By);
        }

        item["createdAt"] = Attr.Timestamp(risk.Created.At);
        item["createdBy"] = Attr.S(risk.Created.By);
        item["updatedAt"] = Attr.Timestamp(risk.Updated.At);
        item["updatedBy"] = Attr.S(risk.Updated.By);
        item["version"] = Attr.N(risk.Version);
        return item;
    }

    public static Risk FromItem(IReadOnlyDictionary<string, AttributeValue> item)
    {
        var fields = new RiskFields(
            Title: Attr.GetString(item, "title"),
            Category: Enum.Parse<RiskCategory>(Attr.GetString(item, "category")),
            Description: Attr.GetStringOrNull(item, "description"),
            Likelihood: (int)Attr.GetLong(item, "likelihood"),
            Impact: (int)Attr.GetLong(item, "impact"),
            Mitigation: Attr.GetStringOrNull(item, "mitigation"),
            Owner: Attr.GetStringOrNull(item, "owner"),
            DueDate: Attr.GetDateOrNull(item, "dueDate"));

        AuditStamp? closed = Attr.GetStringOrNull(item, "closedAt") is not null
            ? new AuditStamp(Attr.GetTimestamp(item, "closedAt"), Attr.GetString(item, "closedBy"))
            : null;

        return Risk.Rehydrate(
            new RiskId(Guid.Parse(Attr.GetString(item, "id"))),
            new ProjectId(Guid.Parse(Attr.GetString(item, "projectId"))),
            fields,
            Enum.Parse<RiskStatus>(Attr.GetString(item, "status")),
            Attr.GetStringOrNull(item, "closingNote"),
            closed,
            new AuditStamp(Attr.GetTimestamp(item, "createdAt"), Attr.GetString(item, "createdBy")),
            new AuditStamp(Attr.GetTimestamp(item, "updatedAt"), Attr.GetString(item, "updatedBy")),
            Attr.GetLong(item, "version"));
    }
}
