using PDS.Shared;

namespace PDS.Sales.Domain;

/// <summary>The current sale. Before Settled, SettlementDate is the due date; at Settled it is the actual date.</summary>
internal sealed record SaleDetails(
    string? BuyerName,
    string? BuyerEmail,
    string? BuyerPhone,
    string? BuyerSolicitor,
    DateOnly? AgreementDate,
    Money? SalePrice,
    Money? Deposit,
    DateOnly? UnconditionalDate,
    DateOnly? SettlementDate);
