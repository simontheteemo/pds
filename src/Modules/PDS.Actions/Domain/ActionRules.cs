namespace PDS.Actions.Domain;

internal static class ActionRules
{
    public const int TitleMaxLength = 200;
    public const int TextMaxLength = 4000;
    public const int LinkLabelMaxLength = 200;

    /// <summary>Owner-index sort key prefix for actions with no due date, so they sort last.</summary>
    public const string NoDueSortKey = "9999-12-31";

    public static ActionFields Normalise(ActionFields f) => f with
    {
        Title = (f.Title ?? string.Empty).Trim(),
        Description = Clean(f.Description),
        Link = f.Link is { } link ? link with { Label = (link.Label ?? string.Empty).Trim() } : null,
    };

    /// <summary>Guards invariants; the API validators report the same rules as field errors first.</summary>
    public static void EnsureValid(ActionFields f)
    {
        if (f.Title.Length == 0)
            throw new ArgumentException("Action title is required.", nameof(f));
        if (string.IsNullOrWhiteSpace(f.OwnerId))
            throw new ArgumentException("An action needs an owner.", nameof(f));
        if (f.Link is { Module: null })
            throw new ArgumentException("A linked record needs a module.", nameof(f));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
