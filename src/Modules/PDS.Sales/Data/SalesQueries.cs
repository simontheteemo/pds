using PDS.Portfolio.Contracts;
using PDS.Sales.Contracts;
using PDS.Sales.Domain;

namespace PDS.Sales.Data;

internal sealed class SalesQueries(UnitStore store) : ISalesQueries
{
    public async Task<IReadOnlyList<ContractedSale>> GetContractedSales(ProjectId projectId, CancellationToken ct)
    {
        var units = await store.ListForProjectAsync(projectId, ct);
        return units
            .Where(u => SaleRules.IsContracted(u.Status))
            .Select(u => new ContractedSale(
                u.Id, u.Fields.Identifier, ToContracted(u.Status),
                u.Sale?.SalePrice?.Amount ?? 0m,   // rules require a price from Conditional on; 0 is defensive
                u.Sale?.SettlementDate))
            .ToList();
    }

    private static ContractedSaleStatus ToContracted(SaleStatus s) => s switch
    {
        SaleStatus.Conditional => ContractedSaleStatus.Conditional,
        SaleStatus.Unconditional => ContractedSaleStatus.Unconditional,
        SaleStatus.Settled => ContractedSaleStatus.Settled,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, "Not a contracted status."),
    };
}
