namespace PDS.Risks.Domain;

internal static class RiskRules
{
    public const int TitleMaxLength = 200;
    public const int TextMaxLength = 4000;
    public const int OwnerMaxLength = 200;
    public const int ClosingNoteMaxLength = 2000;
    public const int MinRating = 1;
    public const int MaxRating = 5;

    public static int Score(int likelihood, int impact) => likelihood * impact;

    public static RiskBand BandFor(int score) => score switch
    {
        <= 4 => RiskBand.Low,
        <= 9 => RiskBand.Medium,
        <= 16 => RiskBand.High,
        _ => RiskBand.Extreme,
    };

    public static bool IsValidRating(int value) => value is >= MinRating and <= MaxRating;

    public static RiskFields Normalise(RiskFields f) => f with
    {
        Title = (f.Title ?? string.Empty).Trim(),
        Description = Clean(f.Description),
        Mitigation = Clean(f.Mitigation),
        Owner = Clean(f.Owner),
    };

    /// <summary>Guards invariants; the API validators report the same rules as field errors first.</summary>
    public static void EnsureValid(RiskFields f)
    {
        if (f.Title.Length == 0)
            throw new ArgumentException("Risk title is required.", nameof(f));
        if (!IsValidRating(f.Likelihood) || !IsValidRating(f.Impact))
            throw new ArgumentException("Likelihood and impact must be between 1 and 5.", nameof(f));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
