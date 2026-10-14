namespace PDS.Sales.Domain;

/// <summary>Any move is allowed: a sale can fall over and go back to Available.</summary>
internal enum SaleStatus
{
    Available,
    Reserved,
    Conditional,
    Unconditional,
    Settled,
    Withdrawn,
}
