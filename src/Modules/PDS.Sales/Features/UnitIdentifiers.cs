using System.Globalization;

namespace PDS.Sales.Features;

internal static class UnitIdentifiers
{
    /// <summary>Numeric-aware and case-insensitive, so "Lot 2" sorts before "Lot 10".</summary>
    public static readonly StringComparer Order =
        StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.NumericOrdering | CompareOptions.IgnoreCase);

    public static bool IsTaken(IEnumerable<Domain.Unit> units, string identifier, Contracts.UnitId? except) =>
        units.Any(u => u.Id != except && string.Equals(u.Fields.Identifier, identifier.Trim(), StringComparison.OrdinalIgnoreCase));
}
