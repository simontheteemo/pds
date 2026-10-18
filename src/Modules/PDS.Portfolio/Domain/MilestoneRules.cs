namespace PDS.Portfolio.Domain;

internal readonly record struct MilestoneEvaluation(MilestoneStatus Status, int? SlippageDays, int? DaysOverdue);

internal static class MilestoneRules
{
    public const int NameMaxLength = 100;
    public const int OwnerMaxLength = 200;
    public const int NotesMaxLength = 2000;
    public const int MaxSortOrder = 9999;

    /// <summary>Display order within a project: sort order, then due date (nulls last), then name.</summary>
    public static IComparer<Milestone> ProjectOrder { get; } = Comparer<Milestone>.Create(static (a, b) =>
    {
        var bySort = a.Fields.SortOrder.CompareTo(b.Fields.SortOrder);
        if (bySort != 0)
            return bySort;
        var byDue = (DueDate(a.Fields), DueDate(b.Fields)) switch
        {
            (null, null) => 0,
            (null, _) => 1,
            (_, null) => -1,
            var (x, y) => x!.Value.CompareTo(y!.Value),
        };
        return byDue != 0 ? byDue : StringComparer.OrdinalIgnoreCase.Compare(a.Fields.Name, b.Fields.Name);
    });

    public static string NameKey(string name) => name.Trim().ToUpperInvariant();

    public static DateOnly? DueDate(MilestoneFields f) => f.ForecastDate ?? f.PlannedDate;

    public static MilestoneEvaluation Evaluate(MilestoneFields f, DateOnly today)
    {
        var effective = f.ActualDate ?? DueDate(f);
        int? slip = f.PlannedDate is { } planned && effective is { } e ? e.DayNumber - planned.DayNumber : null;
        if (f.ActualDate is not null)
            return new(MilestoneStatus.Complete, slip, null);
        if (DueDate(f) is not { } due)
            return new(MilestoneStatus.NotStarted, null, null);
        if (due < today)
            return new(MilestoneStatus.Overdue, slip, today.DayNumber - due.DayNumber);
        if (f.PlannedDate is { } p && f.ForecastDate is { } fc && fc > p)
            return new(MilestoneStatus.AtRisk, slip, null);
        return new(MilestoneStatus.OnTrack, slip, null);
    }

    public static bool IsSlipping(MilestoneStatus s) => s is MilestoneStatus.AtRisk or MilestoneStatus.Overdue;

    public static MilestoneFields Normalise(MilestoneFields f) => f with
    {
        Name = (f.Name ?? string.Empty).Trim(),
        Owner = Clean(f.Owner),
        Notes = Clean(f.Notes),
    };

    /// <summary>Guards invariants; the API validators report the same rules as field errors first.</summary>
    public static void EnsureValid(MilestoneFields f)
    {
        if (f.Name.Length == 0 || f.Name.Length > NameMaxLength)
            throw new ArgumentException("Milestone name is required and at most 100 characters.", nameof(f));
        if (!Enum.IsDefined(f.Category))
            throw new ArgumentException("Milestone category is not valid.", nameof(f));
        if (f.SortOrder is < 0 or > MaxSortOrder)
            throw new ArgumentException("Sort order must be between 0 and 9999.", nameof(f));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
