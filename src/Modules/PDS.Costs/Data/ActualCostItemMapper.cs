using Amazon.DynamoDBv2.Model;
using PDS.Contractors.Contracts;
using PDS.Costs.Contracts;
using PDS.Costs.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared;
using PDS.Shared.Data;

namespace PDS.Costs.Data;

/// <summary>Actual cost item: pk=PROJECT#projectId, sk=ACTUAL#id. Optional attributes are omitted when empty.</summary>
internal static class ActualCostItemMapper
{
    public static string Sk(ActualCostId id) => CostKeys.ActualPrefix + id;

    public static Dictionary<string, AttributeValue> ToItem(ActualCost a)
    {
        var f = a.Fields;
        var item = CostKeys.Key(a.ProjectId, Sk(a.Id));
        item["id"] = Attr.S(a.Id.ToString());
        item["projectId"] = Attr.S(a.ProjectId.Value.ToString());
        item["budgetLineId"] = Attr.S(f.BudgetLineId.ToString());
        item["type"] = Attr.S(f.Type.ToString());
        Attr.SetIfPresent(item, "contractId", f.ContractId?.Value.ToString());
        Attr.SetIfPresent(item, "payee", f.Payee);
        item["description"] = Attr.S(f.Description);
        Attr.SetIfPresent(item, "reference", f.Reference);
        item["date"] = Attr.Date(f.Date);
        item["amount"] = Attr.N(f.Amount.Amount);
        item["currency"] = Attr.S(f.Amount.Currency);
        Attr.SetIfPresent(item, "notes", f.Notes);
        item["createdAt"] = Attr.Timestamp(a.Created.At);
        item["createdBy"] = Attr.S(a.Created.By);
        item["updatedAt"] = Attr.Timestamp(a.Updated.At);
        item["updatedBy"] = Attr.S(a.Updated.By);
        item["version"] = Attr.N(a.Version);
        return item;
    }

    public static ActualCost FromItem(IReadOnlyDictionary<string, AttributeValue> item)
    {
        var contractId = Attr.GetStringOrNull(item, "contractId");
        var fields = new ActualCostFields(
            BudgetLineId: new BudgetLineId(Guid.Parse(Attr.GetString(item, "budgetLineId"))),
            Type: Enum.Parse<ActualCostType>(Attr.GetString(item, "type")),
            ContractId: contractId is null ? null : new ContractId(Guid.Parse(contractId)),
            Payee: Attr.GetStringOrNull(item, "payee"),
            Description: Attr.GetString(item, "description"),
            Reference: Attr.GetStringOrNull(item, "reference"),
            Date: Attr.GetDateOrNull(item, "date") ?? throw new InvalidOperationException("Actual cost is missing its date."),
            Amount: new Money(Attr.GetDecimalOrNull(item, "amount") ?? 0m, Attr.GetString(item, "currency")),
            Notes: Attr.GetStringOrNull(item, "notes"));

        return ActualCost.Rehydrate(
            new ActualCostId(Guid.Parse(Attr.GetString(item, "id"))),
            new ProjectId(Guid.Parse(Attr.GetString(item, "projectId"))),
            fields,
            new AuditStamp(Attr.GetTimestamp(item, "createdAt"), Attr.GetString(item, "createdBy")),
            new AuditStamp(Attr.GetTimestamp(item, "updatedAt"), Attr.GetString(item, "updatedBy")),
            Attr.GetLong(item, "version"));
    }
}
