using Amazon.DynamoDBv2.Model;
using PDS.Contractors.Contracts;
using PDS.Contractors.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared;
using PDS.Shared.Data;

namespace PDS.Contractors.Data;

/// <summary>Contract item: pk=PROJECT#projectId, sk=CONTRACT#id; gsi1pk=CONTRACTOR#contractorId, gsi1sk=CONTRACT#id.</summary>
internal static class ContractItemMapper
{
    public const string ContractPrefix = "CONTRACT#";

    public static string PartitionKey(ProjectId projectId) => $"PROJECT#{projectId.Value}";

    public static string ContractorKey(ContractorId contractorId) => $"CONTRACTOR#{contractorId.Value}";

    public static Dictionary<string, AttributeValue> Key(ProjectId projectId, ContractId id) => new()
    {
        [TableKeys.PartitionKey] = Attr.S(PartitionKey(projectId)),
        [TableKeys.SortKey] = Attr.S($"{ContractPrefix}{id.Value}"),
    };

    public static Dictionary<string, AttributeValue> ToItem(Contract c)
    {
        var f = c.Fields;
        var item = Key(c.ProjectId, c.Id);
        item["gsi1pk"] = Attr.S(ContractorKey(c.ContractorId));
        item["gsi1sk"] = Attr.S($"{ContractPrefix}{c.Id.Value}");
        item["id"] = Attr.S(c.Id.Value.ToString());
        item["projectId"] = Attr.S(c.ProjectId.Value.ToString());
        item["contractorId"] = Attr.S(c.ContractorId.Value.ToString());
        item["title"] = Attr.S(f.Title);
        Attr.SetIfPresent(item, "reference", f.Reference);
        item["form"] = Attr.S(f.Form.ToString());
        if (f.Value is { } money)
        {
            item["valueAmount"] = Attr.N(money.Amount);
            item["currency"] = Attr.S(money.Currency);
        }

        Attr.SetIfPresent(item, "startDate", f.StartDate);
        Attr.SetIfPresent(item, "endDate", f.EndDate);
        Attr.SetIfPresent(item, "retentionPercent", f.RetentionPercent);
        item["status"] = Attr.S(f.Status.ToString());
        Attr.SetIfPresent(item, "notes", f.Notes);
        item["createdAt"] = Attr.Timestamp(c.Created.At);
        item["createdBy"] = Attr.S(c.Created.By);
        item["updatedAt"] = Attr.Timestamp(c.Updated.At);
        item["updatedBy"] = Attr.S(c.Updated.By);
        item["version"] = Attr.N(c.Version);
        return item;
    }

    public static Contract FromItem(IReadOnlyDictionary<string, AttributeValue> item)
    {
        var amount = Attr.GetDecimalOrNull(item, "valueAmount");
        var fields = new ContractFields(
            Title: Attr.GetString(item, "title"),
            Reference: Attr.GetStringOrNull(item, "reference"),
            Form: Enum.Parse<ContractForm>(Attr.GetString(item, "form")),
            Value: amount is { } a ? new Money(a, Attr.GetString(item, "currency")) : null,
            StartDate: Attr.GetDateOrNull(item, "startDate"),
            EndDate: Attr.GetDateOrNull(item, "endDate"),
            RetentionPercent: Attr.GetDecimalOrNull(item, "retentionPercent"),
            Status: Enum.Parse<ContractStatus>(Attr.GetString(item, "status")),
            Notes: Attr.GetStringOrNull(item, "notes"));

        return Contract.Rehydrate(
            new ContractId(Guid.Parse(Attr.GetString(item, "id"))),
            new ProjectId(Guid.Parse(Attr.GetString(item, "projectId"))),
            new ContractorId(Guid.Parse(Attr.GetString(item, "contractorId"))),
            fields,
            new AuditStamp(Attr.GetTimestamp(item, "createdAt"), Attr.GetString(item, "createdBy")),
            new AuditStamp(Attr.GetTimestamp(item, "updatedAt"), Attr.GetString(item, "updatedBy")),
            Attr.GetLong(item, "version"));
    }
}
