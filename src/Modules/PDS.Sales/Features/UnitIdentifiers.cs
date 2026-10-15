namespace PDS.Sales.Features;

internal static class UnitIdentifiers
{
    /// <summary>Numeric-aware and case-insensitive (culture-independent), so "Lot 2" sorts before "Lot 10".</summary>
    public static readonly IComparer<string> Order = new NaturalIdentifierComparer();

    public static bool IsTaken(IEnumerable<Domain.Unit> units, string identifier, Contracts.UnitId? except) =>
        units.Any(u => u.Id != except && string.Equals(u.Fields.Identifier, identifier.Trim(), StringComparison.OrdinalIgnoreCase));
}
