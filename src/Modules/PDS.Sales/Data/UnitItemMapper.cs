using Amazon.DynamoDBv2.Model;
using PDS.Portfolio.Contracts;
using PDS.Sales.Contracts;
using PDS.Sales.Domain;
using PDS.Shared;
using PDS.Shared.Data;

namespace PDS.Sales.Data;

/// <summary>
/// Unit item: pk=PROJECT#projectId, sk=UNIT#unitId, gsi1pk=UNIT, gsi1sk=PROJECT#projectId#UNIT#unitId.
/// All amounts share one currency attribute. Optional attributes are omitted when empty.
/// </summary>
internal static class UnitItemMapper
{
    public const string UnitPrefix = "UNIT#";
    public const string AllUnits = "UNIT";

    public static string PartitionKey(ProjectId projectId) => $"PROJECT#{projectId.Value}";

    public static Dictionary<string, AttributeValue> Key(ProjectId projectId, UnitId unitId) => new()
    {
        [TableKeys.PartitionKey] = Attr.S(PartitionKey(projectId)),
        [TableKeys.SortKey] = Attr.S($"{UnitPrefix}{unitId.Value}"),
    };

    public static Dictionary<string, AttributeValue> ToItem(Unit unit)
    {
        var f = unit.Fields;
        var item = Key(unit.ProjectId, unit.Id);
        item["gsi1pk"] = Attr.S(AllUnits);
        item["gsi1sk"] = Attr.S($"PROJECT#{unit.ProjectId.Value}#{UnitPrefix}{unit.Id.Value}");
        item["id"] = Attr.S(unit.Id.Value.ToString());
        item["projectId"] = Attr.S(unit.ProjectId.Value.ToString());
        item["identifier"] = Attr.S(f.Identifier);
        item["type"] = Attr.S(f.Type.ToString());
        Attr.SetIfPresent(item, "bedrooms", (decimal?)f.Bedrooms);
        Attr.SetIfPresent(item, "bathrooms", (decimal?)f.Bathrooms);
        Attr.SetIfPresent(item, "carParks", (decimal?)f.CarParks);
        Attr.SetIfPresent(item, "floorAreaSqm", f.FloorAreaSqm);
        Attr.SetIfPresent(item, "landAreaSqm", f.LandAreaSqm);
        Attr.SetIfPresent(item, "listPriceAmount", f.ListPrice?.Amount);
        Attr.SetIfPresent(item, "notes", f.Notes);
        item["status"] = Attr.S(unit.Status.ToString());
        if (unit.Sale is { } sale)
        {
            Attr.SetIfPresent(item, "buyerName", sale.BuyerName);
            Attr.SetIfPresent(item, "buyerEmail", sale.BuyerEmail);
            Attr.SetIfPresent(item, "buyerPhone", sale.BuyerPhone);
            Attr.SetIfPresent(item, "buyerSolicitor", sale.BuyerSolicitor);
            Attr.SetIfPresent(item, "agreementDate", sale.AgreementDate);
            Attr.SetIfPresent(item, "salePriceAmount", sale.SalePrice?.Amount);
            Attr.SetIfPresent(item, "depositAmount", sale.Deposit?.Amount);
            Attr.SetIfPresent(item, "unconditionalDate", sale.UnconditionalDate);
            Attr.SetIfPresent(item, "settlementDate", sale.SettlementDate);
        }

        Attr.SetIfPresent(item, "currency", unit.Currency);
        item["createdAt"] = Attr.Timestamp(unit.Created.At);
        item["createdBy"] = Attr.S(unit.Created.By);
        item["updatedAt"] = Attr.Timestamp(unit.Updated.At);
        item["updatedBy"] = Attr.S(unit.Updated.By);
        item["version"] = Attr.N(unit.Version);
        return item;
    }

    public static Unit FromItem(IReadOnlyDictionary<string, AttributeValue> item)
    {
        var fields = new UnitFields(
            Identifier: Attr.GetString(item, "identifier"),
            Type: Enum.Parse<UnitType>(Attr.GetString(item, "type")),
            Bedrooms: (int?)Attr.GetDecimalOrNull(item, "bedrooms"),
            Bathrooms: (int?)Attr.GetDecimalOrNull(item, "bathrooms"),
            CarParks: (int?)Attr.GetDecimalOrNull(item, "carParks"),
            FloorAreaSqm: Attr.GetDecimalOrNull(item, "floorAreaSqm"),
            LandAreaSqm: Attr.GetDecimalOrNull(item, "landAreaSqm"),
            ListPrice: MoneyOrNull(item, "listPriceAmount"),
            Notes: Attr.GetStringOrNull(item, "notes"));

        var status = Enum.Parse<SaleStatus>(Attr.GetString(item, "status"));
        SaleDetails? sale = SaleRules.HasSale(status)
            ? new SaleDetails(
                BuyerName: Attr.GetStringOrNull(item, "buyerName"),
                BuyerEmail: Attr.GetStringOrNull(item, "buyerEmail"),
                BuyerPhone: Attr.GetStringOrNull(item, "buyerPhone"),
                BuyerSolicitor: Attr.GetStringOrNull(item, "buyerSolicitor"),
                AgreementDate: Attr.GetDateOrNull(item, "agreementDate"),
                SalePrice: MoneyOrNull(item, "salePriceAmount"),
                Deposit: MoneyOrNull(item, "depositAmount"),
                UnconditionalDate: Attr.GetDateOrNull(item, "unconditionalDate"),
                SettlementDate: Attr.GetDateOrNull(item, "settlementDate"))
            : null;

        return Unit.Rehydrate(
            new UnitId(Guid.Parse(Attr.GetString(item, "id"))),
            new ProjectId(Guid.Parse(Attr.GetString(item, "projectId"))),
            fields,
            status,
            sale,
            new AuditStamp(Attr.GetTimestamp(item, "createdAt"), Attr.GetString(item, "createdBy")),
            new AuditStamp(Attr.GetTimestamp(item, "updatedAt"), Attr.GetString(item, "updatedBy")),
            Attr.GetLong(item, "version"));
    }

    private static Money? MoneyOrNull(IReadOnlyDictionary<string, AttributeValue> item, string key) =>
        Attr.GetDecimalOrNull(item, key) is { } amount ? new Money(amount, Attr.GetString(item, "currency")) : null;
}
