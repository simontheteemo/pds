namespace PDS.Sales.Contracts;

public sealed record ContractedSale(
    UnitId UnitId, string UnitIdentifier, ContractedSaleStatus Status,
    decimal SalePrice, DateOnly? SettlementDate);
