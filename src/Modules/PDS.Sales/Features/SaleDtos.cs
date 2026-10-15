using PDS.Sales.Domain;
using PDS.Shared;

namespace PDS.Sales.Features;

internal sealed record UpdateSaleRequest(
    SaleStatus? Status, string? BuyerName, string? BuyerEmail, string? BuyerPhone, string? BuyerSolicitor,
    DateOnly? AgreementDate, decimal? SalePriceAmount, decimal? DepositAmount,
    DateOnly? UnconditionalDate, DateOnly? SettlementDate, long Version)
{
    /// <summary>Call only after validation.</summary>
    public SaleDetails ToSale(string currency) => new(
        BuyerName, BuyerEmail, BuyerPhone, BuyerSolicitor, AgreementDate,
        SalePriceAmount is { } p ? new Money(p, currency) : null,
        DepositAmount is { } d ? new Money(d, currency) : null,
        UnconditionalDate, SettlementDate);
}

internal sealed record ProjectSalesSummary(
    Guid ProjectId, string ProjectCode, string ProjectName, bool ProjectArchived, SalesSummary Summary);
