using Amazon.DynamoDBv2.Model;
using PDS.Contractors.Contracts;
using PDS.Costs.Contracts;
using PDS.Costs.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared;
using PDS.Shared.Data;

namespace PDS.Costs.Data;

/// <summary>Commitment item: pk=PROJECT#projectId, sk=COMMIT#id. Optional attributes are omitted when empty.</summary>
internal static class CommitmentItemMapper
{
    public static string Sk(CommitmentId id) => CostKeys.CommitmentPrefix + id;

    public static Dictionary<string, AttributeValue> ToItem(Commitment c)
    {
        var f = c.Fields;
        var item = CostKeys.Key(c.ProjectId, Sk(c.Id));
        item["id"] = Attr.S(c.Id.ToString());
        item["projectId"] = Attr.S(c.ProjectId.Value.ToString());
        item["budgetLineId"] = Attr.S(f.BudgetLineId.ToString());
        item["type"] = Attr.S(f.Type.ToString());
        Attr.SetIfPresent(item, "contractId", f.ContractId?.Value.ToString());
        item["description"] = Attr.S(f.Description);
        Attr.SetIfPresent(item, "reference", f.Reference);
        item["date"] = Attr.Date(f.Date);
        item["amount"] = Attr.N(f.Amount.Amount);
        item["currency"] = Attr.S(f.Amount.Currency);
        Attr.SetIfPresent(item, "notes", f.Notes);
        item["createdAt"] = Attr.Timestamp(c.Created.At);
        item["createdBy"] = Attr.S(c.Created.By);
        item["updatedAt"] = Attr.Timestamp(c.Updated.At);
        item["updatedBy"] = Attr.S(c.Updated.By);
        item["version"] = Attr.N(c.Version);
        return item;
    }

    public static Commitment FromItem(IReadOnlyDictionary<string, AttributeValue> item)
    {
        var contractId = Attr.GetStringOrNull(item, "contractId");
        var fields = new CommitmentFields(
            BudgetLineId: new BudgetLineId(Guid.Parse(Attr.GetString(item, "budgetLineId"))),
            Type: Enum.Parse<CommitmentType>(Attr.GetString(item, "type")),
            ContractId: contractId is null ? null : new ContractId(Guid.Parse(contractId)),
            Description: Attr.GetString(item, "description"),
            Reference: Attr.GetStringOrNull(item, "reference"),
            Date: Attr.GetDateOrNull(item, "date") ?? throw new InvalidOperationException("Commitment is missing its date."),
            Amount: new Money(Attr.GetDecimalOrNull(item, "amount") ?? 0m, Attr.GetString(item, "currency")),
            Notes: Attr.GetStringOrNull(item, "notes"));

        return Commitment.Rehydrate(
            new CommitmentId(Guid.Parse(Attr.GetString(item, "id"))),
            new ProjectId(Guid.Parse(Attr.GetString(item, "projectId"))),
            fields,
            new AuditStamp(Attr.GetTimestamp(item, "createdAt"), Attr.GetString(item, "createdBy")),
            new AuditStamp(Attr.GetTimestamp(item, "updatedAt"), Attr.GetString(item, "updatedBy")),
            Attr.GetLong(item, "version"));
    }
}
