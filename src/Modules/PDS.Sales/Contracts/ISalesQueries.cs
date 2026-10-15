using PDS.Portfolio.Contracts;

namespace PDS.Sales.Contracts;

public interface ISalesQueries
{
    /// <summary>Units with a contracted sale (Conditional, Unconditional, Settled). Empty for an unknown project.</summary>
    Task<IReadOnlyList<ContractedSale>> GetContractedSales(ProjectId projectId, CancellationToken ct);
}
