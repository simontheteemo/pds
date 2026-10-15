namespace PDS.Costs.Domain;

internal static class CostEntryRules
{
    public const int DescriptionMaxLength = 200;
    public const int ReferenceMaxLength = 50;
    public const int PayeeMaxLength = 200;
    public const int NotesMaxLength = 2000;

    public static CommitmentFields Normalise(CommitmentFields f) => f with
    {
        Description = (f.Description ?? string.Empty).Trim(),
        Reference = Clean(f.Reference),
        Notes = Clean(f.Notes),
    };

    public static ActualCostFields Normalise(ActualCostFields f) => f with
    {
        Payee = Clean(f.Payee),
        Description = (f.Description ?? string.Empty).Trim(),
        Reference = Clean(f.Reference),
        Notes = Clean(f.Notes),
    };

    /// <summary>Guards invariants; the API validators report the same rules as field errors first.</summary>
    public static void EnsureValid(CommitmentFields f)
    {
        if (f.Description.Length == 0)
            throw new ArgumentException("Description is required.", nameof(f));
        if (!MoneyRules.IsValidAmount(f.Amount.Amount))
            throw new ArgumentException("Amount must have at most 2 decimal places.", nameof(f));
        if (f.Amount.Amount < 0 && f.Type != CommitmentType.Variation)
            throw new ArgumentException("Only a variation can be negative.", nameof(f));
    }

    public static void EnsureValid(ActualCostFields f)
    {
        if (f.Description.Length == 0)
            throw new ArgumentException("Description is required.", nameof(f));
        if (!MoneyRules.IsValidAmount(f.Amount.Amount))
            throw new ArgumentException("Amount must have at most 2 decimal places.", nameof(f));
        if (f.Amount.Amount == 0)
            throw new ArgumentException("Amount can't be zero.", nameof(f));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
