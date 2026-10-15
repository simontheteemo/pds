namespace PDS.Sales.Domain;

/// <summary>Counts by status and values for a set of units. Values are amounts in the deployment currency.</summary>
internal sealed record SalesTotals(
    int TotalUnits,
    int Available,
    int Reserved,
    int Conditional,
    int Unconditional,
    int Settled,
    int Withdrawn,
    decimal ListValue,
    decimal ContractedValue,
    decimal SettledValue)
{
    public static SalesTotals For(IEnumerable<Unit> units)
    {
        var list = units.ToList();
        int Count(SaleStatus s) => list.Count(u => u.Status == s);
        return new SalesTotals(
            list.Count,
            Count(SaleStatus.Available),
            Count(SaleStatus.Reserved),
            Count(SaleStatus.Conditional),
            Count(SaleStatus.Unconditional),
            Count(SaleStatus.Settled),
            Count(SaleStatus.Withdrawn),
            ListValue: list.Where(u => u.Status != SaleStatus.Withdrawn).Sum(u => u.Fields.ListPrice?.Amount ?? 0m),
            ContractedValue: list.Where(u => SaleRules.IsContracted(u.Status)).Sum(u => u.Sale?.SalePrice?.Amount ?? 0m),
            SettledValue: list.Where(u => u.Status == SaleStatus.Settled).Sum(u => u.Sale?.SalePrice?.Amount ?? 0m));
    }
}
