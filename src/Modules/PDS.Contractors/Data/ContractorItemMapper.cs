using Amazon.DynamoDBv2.Model;
using PDS.Contractors.Contracts;
using PDS.Contractors.Domain;
using PDS.Shared.Data;

namespace PDS.Contractors.Data;

/// <summary>Contractor item: pk=CONTRACTOR#id, sk=PROFILE; gsi1pk=CONTRACTOR for listing, gsi1sk=name#id.</summary>
internal static class ContractorItemMapper
{
    public const string ContractorType = "CONTRACTOR";

    public static Dictionary<string, AttributeValue> Key(ContractorId id) => new()
    {
        [TableKeys.PartitionKey] = Attr.S($"{ContractorType}#{id.Value}"),
        [TableKeys.SortKey] = Attr.S("PROFILE"),
    };

    public static Dictionary<string, AttributeValue> ToItem(Contractor c)
    {
        var f = c.Fields;
        var item = Key(c.Id);
        item["gsi1pk"] = Attr.S(ContractorType);
        item["gsi1sk"] = Attr.S($"{f.Name.ToLowerInvariant()}#{c.Id}");
        item["id"] = Attr.S(c.Id.Value.ToString());
        item["name"] = Attr.S(f.Name);
        item["type"] = Attr.S(f.Type.ToString());
        Attr.SetIfPresent(item, "trade", f.Trade);
        Attr.SetIfPresent(item, "nzbn", f.Nzbn);
        Attr.SetIfPresent(item, "contactName", f.ContactName);
        Attr.SetIfPresent(item, "email", f.Email);
        Attr.SetIfPresent(item, "phone", f.Phone);
        Attr.SetIfPresent(item, "address", f.Address);
        Attr.SetIfPresent(item, "publicLiabilityExpiry", f.PublicLiabilityExpiry);
        Attr.SetIfPresent(item, "professionalIndemnityExpiry", f.ProfessionalIndemnityExpiry);
        Attr.SetIfPresent(item, "licenceNumber", f.LicenceNumber);
        Attr.SetIfPresent(item, "licenceExpiry", f.LicenceExpiry);
        Attr.SetIfPresent(item, "notes", f.Notes);
        item["isActive"] = Attr.Bool(c.IsActive);
        item["createdAt"] = Attr.Timestamp(c.Created.At);
        item["createdBy"] = Attr.S(c.Created.By);
        item["updatedAt"] = Attr.Timestamp(c.Updated.At);
        item["updatedBy"] = Attr.S(c.Updated.By);
        item["version"] = Attr.N(c.Version);
        return item;
    }

    public static Contractor FromItem(IReadOnlyDictionary<string, AttributeValue> item)
    {
        var fields = new ContractorFields(
            Name: Attr.GetString(item, "name"),
            Type: Enum.Parse<Domain.ContractorType>(Attr.GetString(item, "type")),
            Trade: Attr.GetStringOrNull(item, "trade"),
            Nzbn: Attr.GetStringOrNull(item, "nzbn"),
            ContactName: Attr.GetStringOrNull(item, "contactName"),
            Email: Attr.GetStringOrNull(item, "email"),
            Phone: Attr.GetStringOrNull(item, "phone"),
            Address: Attr.GetStringOrNull(item, "address"),
            PublicLiabilityExpiry: Attr.GetDateOrNull(item, "publicLiabilityExpiry"),
            ProfessionalIndemnityExpiry: Attr.GetDateOrNull(item, "professionalIndemnityExpiry"),
            LicenceNumber: Attr.GetStringOrNull(item, "licenceNumber"),
            LicenceExpiry: Attr.GetDateOrNull(item, "licenceExpiry"),
            Notes: Attr.GetStringOrNull(item, "notes"));

        // Tolerant read: a missing isActive means active.
        var isActive = !item.TryGetValue("isActive", out var active) || active.BOOL != false;

        return Contractor.Rehydrate(
            new ContractorId(Guid.Parse(Attr.GetString(item, "id"))),
            fields,
            isActive,
            new AuditStamp(Attr.GetTimestamp(item, "createdAt"), Attr.GetString(item, "createdBy")),
            new AuditStamp(Attr.GetTimestamp(item, "updatedAt"), Attr.GetString(item, "updatedBy")),
            Attr.GetLong(item, "version"));
    }
}
