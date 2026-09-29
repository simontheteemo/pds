using Amazon.DynamoDBv2.Model;
using PDS.Portfolio.Contracts;
using PDS.Portfolio.Domain;
using PDS.Shared;
using PDS.Shared.Data;

namespace PDS.Portfolio.Data;

/// <summary>
/// Project item: pk=PROJECT#id, sk=PROJECT, gsi1pk=PROJECT, gsi1sk=code.
/// Code guard item: pk=CODE#code, sk=CODE, projectId. Optional attributes are omitted when empty.
/// </summary>
internal static class ProjectItemMapper
{
    public const string ProjectType = "PROJECT";

    public static Dictionary<string, AttributeValue> Key(ProjectId id) => new()
    {
        [TableKeys.PartitionKey] = Attr.S($"PROJECT#{id.Value}"),
        [TableKeys.SortKey] = Attr.S(ProjectType),
    };

    public static Dictionary<string, AttributeValue> CodeGuardKey(string code) => new()
    {
        [TableKeys.PartitionKey] = Attr.S($"CODE#{code}"),
        [TableKeys.SortKey] = Attr.S("CODE"),
    };

    public static Dictionary<string, AttributeValue> CodeGuard(Project project)
    {
        var item = CodeGuardKey(project.Fields.Code);
        item["projectId"] = Attr.S(project.Id.Value.ToString());
        return item;
    }

    public static Dictionary<string, AttributeValue> ToItem(Project project)
    {
        var f = project.Fields;
        var item = Key(project.Id);
        item["gsi1pk"] = Attr.S(ProjectType);
        item["gsi1sk"] = Attr.S(f.Code);
        item["id"] = Attr.S(project.Id.Value.ToString());
        item["code"] = Attr.S(f.Code);
        item["name"] = Attr.S(f.Name);
        item["site"] = Attr.Map(SiteToMap(f.Site));
        item["stage"] = Attr.S(f.Stage.ToString());
        item["status"] = Attr.S(f.Status.ToString());
        Attr.SetIfPresent(item, "plannedStart", f.PlannedStart);
        Attr.SetIfPresent(item, "plannedCompletion", f.PlannedCompletion);
        Attr.SetIfPresent(item, "actualStart", f.ActualStart);
        Attr.SetIfPresent(item, "actualCompletion", f.ActualCompletion);
        if (f.Budget is { } budget)
        {
            item["budgetAmount"] = Attr.N(budget.Amount);
            item["currency"] = Attr.S(budget.Currency);
        }

        Attr.SetIfPresent(item, "projectManager", f.ProjectManager);
        Attr.SetIfPresent(item, "description", f.Description);
        item["isArchived"] = Attr.Bool(project.IsArchived);
        item["createdAt"] = Attr.Timestamp(project.Created.At);
        item["createdBy"] = Attr.S(project.Created.By);
        item["updatedAt"] = Attr.Timestamp(project.Updated.At);
        item["updatedBy"] = Attr.S(project.Updated.By);
        item["version"] = Attr.N(project.Version);
        return item;
    }

    public static Project FromItem(IReadOnlyDictionary<string, AttributeValue> item)
    {
        var site = Attr.GetMap(item, "site");
        var budgetAmount = Attr.GetDecimalOrNull(item, "budgetAmount");
        var fields = new ProjectFields(
            Code: Attr.GetString(item, "code"),
            Name: Attr.GetString(item, "name"),
            Site: new Site(
                Attr.GetString(site, "addressLine"),
                Attr.GetStringOrNull(site, "suburb"),
                Attr.GetString(site, "city"),
                Attr.GetStringOrNull(site, "region"),
                Attr.GetStringOrNull(site, "postcode"),
                Attr.GetStringOrNull(site, "legalDescription"),
                Attr.GetStringOrNull(site, "titleReference"),
                Attr.GetDecimalOrNull(site, "landAreaSqm")),
            Stage: Enum.Parse<ProjectStage>(Attr.GetString(item, "stage")),
            Status: Enum.Parse<ProjectStatus>(Attr.GetString(item, "status")),
            PlannedStart: Attr.GetDateOrNull(item, "plannedStart"),
            PlannedCompletion: Attr.GetDateOrNull(item, "plannedCompletion"),
            ActualStart: Attr.GetDateOrNull(item, "actualStart"),
            ActualCompletion: Attr.GetDateOrNull(item, "actualCompletion"),
            Budget: budgetAmount is { } amount ? new Money(amount, Attr.GetString(item, "currency")) : null,
            ProjectManager: Attr.GetStringOrNull(item, "projectManager"),
            Description: Attr.GetStringOrNull(item, "description"));

        return Project.Rehydrate(
            new ProjectId(Guid.Parse(Attr.GetString(item, "id"))),
            fields,
            Attr.GetBool(item, "isArchived"),
            new AuditStamp(Attr.GetTimestamp(item, "createdAt"), Attr.GetString(item, "createdBy")),
            new AuditStamp(Attr.GetTimestamp(item, "updatedAt"), Attr.GetString(item, "updatedBy")),
            Attr.GetLong(item, "version"));
    }

    private static Dictionary<string, AttributeValue> SiteToMap(Site site)
    {
        var map = new Dictionary<string, AttributeValue>
        {
            ["addressLine"] = Attr.S(site.AddressLine),
            ["city"] = Attr.S(site.City),
        };
        Attr.SetIfPresent(map, "suburb", site.Suburb);
        Attr.SetIfPresent(map, "region", site.Region);
        Attr.SetIfPresent(map, "postcode", site.Postcode);
        Attr.SetIfPresent(map, "legalDescription", site.LegalDescription);
        Attr.SetIfPresent(map, "titleReference", site.TitleReference);
        Attr.SetIfPresent(map, "landAreaSqm", site.LandAreaSqm);
        return map;
    }
}
