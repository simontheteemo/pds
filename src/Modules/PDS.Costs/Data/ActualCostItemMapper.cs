using System.Globalization;
using Amazon.DynamoDBv2.Model;
using PDS.Contractors.Contracts;
using PDS.Costs.Contracts;
using PDS.Costs.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared;
using PDS.Shared.Data;

namespace PDS.Costs.Data;

/// <summary>
/// Actual cost item: pk=PROJECT#projectId, sk=ACTUAL#id. Optional attributes are omitted when empty. With a contractor
/// it also carries gsi1pk=CONTRACTOR#contractorId, gsi1sk=ACTUAL#date#id. Older items without the invoice attributes
/// read as status Received with no contractor, dates or GST.
/// </summary>
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
        Attr.SetIfPresent(item, "contractorId", f.ContractorId?.Value.ToString());
        if (f.ContractorId is { } contractorId)
        {
            item["gsi1pk"] = Attr.S(CostKeys.ContractorKey(contractorId));
            item["gsi1sk"] = Attr.S(string.Create(CultureInfo.InvariantCulture, $"{CostKeys.ActualPrefix}{f.Date:yyyy-MM-dd}#{a.Id}"));
        }
        Attr.SetIfPresent(item, "payee", f.Payee);
        item["description"] = Attr.S(f.Description);
        Attr.SetIfPresent(item, "reference", f.Reference);
        item["date"] = Attr.Date(f.Date);
        Attr.SetIfPresent(item, "dueDate", f.DueDate);
        item["status"] = Attr.S(f.Status.ToString());
        Attr.SetIfPresent(item, "paidDate", f.PaidDate);
        item["amount"] = Attr.N(f.Amount.Amount);
        item["currency"] = Attr.S(f.Amount.Currency);
        Attr.SetIfPresent(item, "gstAmount", f.Gst?.Amount);
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
        var contractorId = Attr.GetStringOrNull(item, "contractorId");
        var status = Attr.GetStringOrNull(item, "status");
        var currency = Attr.GetString(item, "currency");
        var gst = Attr.GetDecimalOrNull(item, "gstAmount");
        var fields = new ActualCostFields(
            BudgetLineId: new BudgetLineId(Guid.Parse(Attr.GetString(item, "budgetLineId"))),
            Type: Enum.Parse<ActualCostType>(Attr.GetString(item, "type")),
            ContractId: contractId is null ? null : new ContractId(Guid.Parse(contractId)),
            ContractorId: contractorId is null ? null : new ContractorId(Guid.Parse(contractorId)),
            Payee: Attr.GetStringOrNull(item, "payee"),
            Description: Attr.GetString(item, "description"),
            Reference: Attr.GetStringOrNull(item, "reference"),
            Date: Attr.GetDateOrNull(item, "date") ?? throw new InvalidOperationException("Actual cost is missing its date."),
            DueDate: Attr.GetDateOrNull(item, "dueDate"),
            Status: status is null ? InvoiceStatus.Received : Enum.Parse<InvoiceStatus>(status),
            PaidDate: Attr.GetDateOrNull(item, "paidDate"),
            Amount: new Money(Attr.GetDecimalOrNull(item, "amount") ?? 0m, currency),
            Gst: gst is { } g ? new Money(g, currency) : null,
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
