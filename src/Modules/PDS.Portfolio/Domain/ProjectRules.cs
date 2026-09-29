using System.Text.RegularExpressions;

namespace PDS.Portfolio.Domain;

internal static partial class ProjectRules
{
    public const int NameMaxLength = 200;
    public const int AddressMaxLength = 200;
    public const int CityMaxLength = 100;
    public const int ShortTextMaxLength = 100;
    public const int PostcodeMaxLength = 20;
    public const int LegalDescriptionMaxLength = 500;
    public const int ProjectManagerMaxLength = 200;
    public const int DescriptionMaxLength = 4000;

    public static string NormaliseCode(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    public static bool IsValidCode(string normalisedCode) => CodePattern().IsMatch(normalisedCode);

    public static bool IsOrdered(DateOnly? start, DateOnly? end) => start is null || end is null || end >= start;

    public static ProjectFields Normalise(ProjectFields fields) => fields with
    {
        Code = NormaliseCode(fields.Code),
        Name = (fields.Name ?? string.Empty).Trim(),
        Site = fields.Site with
        {
            AddressLine = (fields.Site.AddressLine ?? string.Empty).Trim(),
            City = (fields.Site.City ?? string.Empty).Trim(),
            Suburb = Clean(fields.Site.Suburb),
            Region = Clean(fields.Site.Region),
            Postcode = Clean(fields.Site.Postcode),
            LegalDescription = Clean(fields.Site.LegalDescription),
            TitleReference = Clean(fields.Site.TitleReference),
        },
        ProjectManager = Clean(fields.ProjectManager),
        Description = Clean(fields.Description),
    };

    /// <summary>Guards invariants. The API validators report the same rules as field errors before this runs.</summary>
    public static void EnsureValid(ProjectFields fields)
    {
        if (!IsValidCode(fields.Code))
            throw new ArgumentException($"Invalid project code '{fields.Code}'.", nameof(fields));
        if (fields.Name.Length == 0)
            throw new ArgumentException("Project name is required.", nameof(fields));
        if (fields.Site.AddressLine.Length == 0 || fields.Site.City.Length == 0)
            throw new ArgumentException("Site address and city are required.", nameof(fields));
        if (!IsOrdered(fields.PlannedStart, fields.PlannedCompletion) || !IsOrdered(fields.ActualStart, fields.ActualCompletion))
            throw new ArgumentException("Completion dates must be on or after start dates.", nameof(fields));
        if (fields.Budget is { Amount: < 0 })
            throw new ArgumentException("Budget cannot be negative.", nameof(fields));
        if (fields.Site.LandAreaSqm is < 0)
            throw new ArgumentException("Land area cannot be negative.", nameof(fields));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("^[A-Z0-9][A-Z0-9-]{0,19}$")]
    private static partial Regex CodePattern();
}
