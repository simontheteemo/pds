using Amazon.DynamoDBv2.Model;
using PDS.Costs.Contracts;
using PDS.Costs.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared;
using PDS.Shared.Data;

namespace PDS.Costs.Data;

/// <summary>Budget line item: pk=PROJECT#projectId, sk=LINE#lineId. Optional attributes are omitted when empty.</summary>
internal static class BudgetLineItemMapper
{
    public static string Sk(BudgetLineId id) => CostKeys.LinePrefix + id;

    public static Dictionary<string, AttributeValue> ToItem(BudgetLine line)
    {
        var f = line.Fields;
        var item = CostKeys.Key(line.ProjectId, Sk(line.Id));
        item["id"] = Attr.S(line.Id.ToString());
        item["projectId"] = Attr.S(line.ProjectId.Value.ToString());
        item["name"] = Attr.S(f.Name);
        item["sortOrder"] = Attr.N(f.SortOrder);
        item["budgetAmount"] = Attr.N(f.Budget.Amount);
        item["currency"] = Attr.S(f.Budget.Currency);
        Attr.SetIfPresent(item, "forecastRemainingAmount", f.ForecastRemaining?.Amount);
        Attr.SetIfPresent(item, "notes", f.Notes);
        item["createdAt"] = Attr.Timestamp(line.Created.At);
        item["createdBy"] = Attr.S(line.Created.By);
        item["updatedAt"] = Attr.Timestamp(line.Updated.At);
        item["updatedBy"] = Attr.S(line.Updated.By);
        item["version"] = Attr.N(line.Version);
        return item;
    }

    public static BudgetLine FromItem(IReadOnlyDictionary<string, AttributeValue> item)
    {
        var currency = Attr.GetString(item, "currency");
        var forecast = Attr.GetDecimalOrNull(item, "forecastRemainingAmount");
        var fields = new BudgetLineFields(
            Name: Attr.GetString(item, "name"),
            SortOrder: (int)Attr.GetLong(item, "sortOrder"),
            Budget: new Money(Attr.GetDecimalOrNull(item, "budgetAmount") ?? 0m, currency),
            ForecastRemaining: forecast is { } r ? new Money(r, currency) : null,
            Notes: Attr.GetStringOrNull(item, "notes"));

        return BudgetLine.Rehydrate(
            new BudgetLineId(Guid.Parse(Attr.GetString(item, "id"))),
            new ProjectId(Guid.Parse(Attr.GetString(item, "projectId"))),
            fields,
            new AuditStamp(Attr.GetTimestamp(item, "createdAt"), Attr.GetString(item, "createdBy")),
            new AuditStamp(Attr.GetTimestamp(item, "updatedAt"), Attr.GetString(item, "updatedBy")),
            Attr.GetLong(item, "version"));
    }
}
