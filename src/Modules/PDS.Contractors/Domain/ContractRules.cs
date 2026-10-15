namespace PDS.Contractors.Domain;

internal static class ContractRules
{
    public const int TitleMaxLength = 200;
    public const int ReferenceMaxLength = 50;
    public const int NotesMaxLength = 4000;
    public const decimal MaxRetentionPercent = 20m;

    public static bool IsOrdered(DateOnly? start, DateOnly? end) =>
        start is not { } s || end is not { } e || e >= s;

    public static ContractFields Normalise(ContractFields f) => f with
    {
        Title = (f.Title ?? string.Empty).Trim(),
        Reference = Clean(f.Reference),
        Notes = Clean(f.Notes),
    };

    /// <summary>Guards invariants; the API validators report the same rules as field errors first.</summary>
    public static void EnsureValid(ContractFields f)
    {
        if (f.Title.Length == 0)
            throw new ArgumentException("Contract title is required.", nameof(f));
        if (f.Value is { Amount: < 0 })
            throw new ArgumentException("Contract value can't be negative.", nameof(f));
        if (f.RetentionPercent is { } r && (r < 0 || r > MaxRetentionPercent || decimal.Round(r, 1) != r))
            throw new ArgumentException("Retention must be between 0 and 20 with at most 1 decimal place.", nameof(f));
        if (!IsOrdered(f.StartDate, f.EndDate))
            throw new ArgumentException("End date can't be before the start date.", nameof(f));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
