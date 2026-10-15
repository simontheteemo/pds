namespace PDS.Sales.Domain;

internal static class UnitRules
{
    public const int IdentifierMaxLength = 50;
    public const int NotesMaxLength = 4000;
    public const int MaxRoomCount = 20;
    public const decimal MaxAreaSqm = 1_000_000m;

    public static UnitFields Normalise(UnitFields f) => f with
    {
        Identifier = (f.Identifier ?? string.Empty).Trim(),
        Notes = Clean(f.Notes),
    };

    /// <summary>Guards invariants; the API validators report the same rules as field errors first.</summary>
    public static void EnsureValid(UnitFields f)
    {
        if (f.Identifier.Length == 0)
            throw new ArgumentException("Unit identifier is required.", nameof(f));
        if (f.ListPrice is { Amount: < 0 })
            throw new ArgumentException("List price cannot be negative.", nameof(f));
    }

    internal static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
