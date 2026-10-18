using Amazon.DynamoDBv2.Model;
using PDS.Portfolio.Contracts;
using PDS.Portfolio.Domain;
using PDS.Shared.Data;

namespace PDS.Portfolio.Data;

/// <summary>
/// Milestone item: pk=PROJECT#projectId (same partition as the project item), sk=MILESTONE#milestoneId,
/// gsi1pk=MILESTONE, gsi1sk=projectId#milestoneId. Optional attributes are omitted when empty.
/// </summary>
internal static class MilestoneItemMapper
{
    public const string MilestoneType = "MILESTONE";
    public const string SkPrefix = "MILESTONE#";

    public static string Pk(ProjectId projectId) => $"PROJECT#{projectId.Value}";

    public static string Sk(MilestoneId id) => SkPrefix + id;

    public static Dictionary<string, AttributeValue> Key(ProjectId projectId, MilestoneId id) => new()
    {
        [TableKeys.PartitionKey] = Attr.S(Pk(projectId)),
        [TableKeys.SortKey] = Attr.S(Sk(id)),
    };

    public static Dictionary<string, AttributeValue> ToItem(Milestone m)
    {
        var f = m.Fields;
        var item = Key(m.ProjectId, m.Id);
        item["gsi1pk"] = Attr.S(MilestoneType);
        item["gsi1sk"] = Attr.S($"{m.ProjectId}#{m.Id}");
        item["id"] = Attr.S(m.Id.ToString());
        item["projectId"] = Attr.S(m.ProjectId.ToString());
        item["name"] = Attr.S(f.Name);
        item["category"] = Attr.S(f.Category.ToString());
        Attr.SetIfPresent(item, "plannedDate", f.PlannedDate);
        Attr.SetIfPresent(item, "forecastDate", f.ForecastDate);
        Attr.SetIfPresent(item, "actualDate", f.ActualDate);
        Attr.SetIfPresent(item, "owner", f.Owner);
        Attr.SetIfPresent(item, "notes", f.Notes);
        item["sortOrder"] = Attr.N((long)f.SortOrder);
        item["createdAt"] = Attr.Timestamp(m.Created.At);
        item["createdBy"] = Attr.S(m.Created.By);
        item["updatedAt"] = Attr.Timestamp(m.Updated.At);
        item["updatedBy"] = Attr.S(m.Updated.By);
        item["version"] = Attr.N(m.Version);
        return item;
    }

    public static Milestone FromItem(IReadOnlyDictionary<string, AttributeValue> item)
    {
        var fields = new MilestoneFields(
            Name: Attr.GetString(item, "name"),
            Category: Enum.Parse<MilestoneCategory>(Attr.GetString(item, "category")),
            PlannedDate: Attr.GetDateOrNull(item, "plannedDate"),
            ForecastDate: Attr.GetDateOrNull(item, "forecastDate"),
            ActualDate: Attr.GetDateOrNull(item, "actualDate"),
            Owner: Attr.GetStringOrNull(item, "owner"),
            Notes: Attr.GetStringOrNull(item, "notes"),
            SortOrder: (int)Attr.GetLong(item, "sortOrder"));

        return Milestone.Rehydrate(
            new MilestoneId(Guid.Parse(Attr.GetString(item, "id"))),
            new ProjectId(Guid.Parse(Attr.GetString(item, "projectId"))),
            fields,
            new AuditStamp(Attr.GetTimestamp(item, "createdAt"), Attr.GetString(item, "createdBy")),
            new AuditStamp(Attr.GetTimestamp(item, "updatedAt"), Attr.GetString(item, "updatedBy")),
            Attr.GetLong(item, "version"));
    }
}
