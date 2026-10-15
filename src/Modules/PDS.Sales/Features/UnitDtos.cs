using PDS.Sales.Domain;
using PDS.Shared;

namespace PDS.Sales.Features;

/// <summary>Editable unit fields. Type is nullable so omission is a 400, not a default.</summary>
internal interface IUnitInput
{
    string Identifier { get; }

    UnitType? Type { get; }

    int? Bedrooms { get; }

    int? Bathrooms { get; }

    int? CarParks { get; }

    decimal? FloorAreaSqm { get; }

    decimal? LandAreaSqm { get; }

    decimal? ListPriceAmount { get; }

    string? Notes { get; }
}

internal sealed record CreateUnitRequest(
    string Identifier, UnitType? Type, int? Bedrooms, int? Bathrooms, int? CarParks,
    decimal? FloorAreaSqm, decimal? LandAreaSqm, decimal? ListPriceAmount, string? Notes) : IUnitInput;

internal sealed record UpdateUnitRequest(
    string Identifier, UnitType? Type, int? Bedrooms, int? Bathrooms, int? CarParks,
    decimal? FloorAreaSqm, decimal? LandAreaSqm, decimal? ListPriceAmount, string? Notes, long Version) : IUnitInput;

internal static class UnitInputMapping
{
    /// <summary>Call only after validation: type is known to be present.</summary>
    public static UnitFields ToFields(this IUnitInput input, string currency) => new(
        input.Identifier,
        input.Type!.Value,
        input.Bedrooms,
        input.Bathrooms,
        input.CarParks,
        input.FloorAreaSqm,
        input.LandAreaSqm,
        input.ListPriceAmount is { } amount ? new Money(amount, currency) : null,
        input.Notes);
}

internal sealed record UnitDetails(
    Guid Id,
    Guid ProjectId,
    string Identifier,
    UnitType Type,
    int? Bedrooms,
    int? Bathrooms,
    int? CarParks,
    decimal? FloorAreaSqm,
    decimal? LandAreaSqm,
    decimal? ListPriceAmount,
    string? Notes,
    SaleStatus Status,
    string? BuyerName,
    string? BuyerEmail,
    string? BuyerPhone,
    string? BuyerSolicitor,
    DateOnly? AgreementDate,
    decimal? SalePriceAmount,
    decimal? DepositAmount,
    DateOnly? UnconditionalDate,
    DateOnly? SettlementDate,
    string Currency,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    long Version)
{
    public static UnitDetails From(Unit u, string defaultCurrency)
    {
        var f = u.Fields;
        var s = u.Sale;
        return new UnitDetails(
            u.Id.Value, u.ProjectId.Value, f.Identifier, f.Type, f.Bedrooms, f.Bathrooms, f.CarParks,
            f.FloorAreaSqm, f.LandAreaSqm, f.ListPrice?.Amount, f.Notes, u.Status,
            s?.BuyerName, s?.BuyerEmail, s?.BuyerPhone, s?.BuyerSolicitor, s?.AgreementDate,
            s?.SalePrice?.Amount, s?.Deposit?.Amount, s?.UnconditionalDate, s?.SettlementDate,
            u.Currency ?? defaultCurrency, u.Created.At, u.Created.By, u.Updated.At, u.Updated.By, u.Version);
    }
}

internal sealed record SalesSummary(
    int TotalUnits,
    int Available,
    int Reserved,
    int Conditional,
    int Unconditional,
    int Settled,
    int Withdrawn,
    decimal ListValue,
    decimal ContractedValue,
    decimal SettledValue,
    string Currency)
{
    public static SalesSummary From(SalesTotals t, string currency) => new(
        t.TotalUnits, t.Available, t.Reserved, t.Conditional, t.Unconditional, t.Settled, t.Withdrawn,
        t.ListValue, t.ContractedValue, t.SettledValue, currency);
}

internal sealed record ProjectSales(SalesSummary Summary, List<UnitDetails> Units);
