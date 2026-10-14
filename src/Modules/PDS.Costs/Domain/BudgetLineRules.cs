namespace PDS.Costs.Domain;

internal static class BudgetLineRules
{
    public const int NameMaxLength = 100;
    public const int NotesMaxLength = 2000;
    public const int MaxSortOrder = 9999;

    public static string NameKey(string name) => name.Trim().ToUpperInvariant();

    public static BudgetLineFields Normalise(BudgetLineFields f) => f with
    {
        Name = (f.Name ?? string.Empty).Trim(),
        Notes = string.IsNullOrWhiteSpace(f.Notes) ? null : f.Notes.Trim(),
    };

    /// <summary>Guards invariants; the API validators report the same rules as field errors first.</summary>
    public static void EnsureValid(BudgetLineFields f)
    {
        if (f.Name.Length == 0)
            throw new ArgumentException("Budget line name is required.", nameof(f));
        if (f.SortOrder is < 0 or > MaxSortOrder)
            throw new ArgumentException("Sort order must be between 0 and 9999.", nameof(f));
        if (f.Budget.Amount < 0 || !MoneyRules.IsValidAmount(f.Budget.Amount))
            throw new ArgumentException("Budget must be a non-negative amount with at most 2 decimal places.", nameof(f));
        if (f.ForecastRemaining is { } r && (r.Amount < 0 || !MoneyRules.IsValidAmount(r.Amount)))
            throw new ArgumentException("Forecast remaining must be a non-negative amount with at most 2 decimal places.", nameof(f));
    }
}
