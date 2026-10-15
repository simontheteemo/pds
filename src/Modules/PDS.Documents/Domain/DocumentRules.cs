namespace PDS.Documents.Domain;

internal static class DocumentRules
{
    public const int TitleMaxLength = 200;
    public const int FileNameMaxLength = 255;
    public const int MaxTags = 10;
    public const int TagMaxLength = 40;
    public const int NotesMaxLength = 2000;
    public const int LinkLabelMaxLength = 200;

    public static DocumentFields Normalise(DocumentFields f, IReadOnlyList<string> categories) => f with
    {
        Title = f.Title.Trim(),
        // Store the configured spelling of the category.
        Category = categories.FirstOrDefault(c => string.Equals(c, f.Category.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? f.Category.Trim(),
        Link = f.Link is { } link ? link with { Label = link.Label.Trim() } : null,
        Tags = NormaliseTags(f.Tags),
        Notes = string.IsNullOrWhiteSpace(f.Notes) ? null : f.Notes.Trim(),
    };

    /// <summary>Trimmed, blanks dropped, de-duplicated case-insensitively (first spelling kept).</summary>
    public static IReadOnlyList<string> NormaliseTags(IEnumerable<string>? tags) =>
        (tags ?? []).Select(t => t?.Trim() ?? string.Empty).Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
