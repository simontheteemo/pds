namespace PDS.Consents.Domain;

internal static class ConsentRules
{
    public const int TitleMaxLength = 200;
    public const int AuthorityMaxLength = 200;
    public const int ReferenceMaxLength = 100;
    public const int NotesMaxLength = 4000;
    public const int ConditionReferenceMaxLength = 50;
    public const int ConditionDescriptionMaxLength = 2000;
    public const int ConditionWarningDays = 30;
    public const int ExpiryWarningDays = 90;

    public static bool IsActive(ConsentStatus status) => status is not (ConsentStatus.Declined or ConsentStatus.Withdrawn);

    public static bool IsOrdered(DateOnly? earlier, DateOnly? later) => earlier is null || later is null || later >= earlier;

    public static ConsentFields Normalise(ConsentFields f) => f with
    {
        Title = (f.Title ?? string.Empty).Trim(),
        Authority = Clean(f.Authority),
        Reference = Clean(f.Reference),
        Notes = Clean(f.Notes),
    };

    public static ConditionFields Normalise(ConditionFields f) => f with
    {
        Reference = Clean(f.Reference),
        Description = (f.Description ?? string.Empty).Trim(),
    };

    /// <summary>Guards invariants; the API validators report the same rules as field errors first.</summary>
    public static void EnsureValid(ConsentFields f)
    {
        if (f.Title.Length == 0)
            throw new ArgumentException("Consent title is required.", nameof(f));
        if (f.Status == ConsentStatus.Granted && f.GrantedDate is null)
            throw new ArgumentException("A granted consent needs a granted date.", nameof(f));
        if (!IsOrdered(f.LodgedDate, f.GrantedDate) || !IsOrdered(f.GrantedDate, f.ExpiryDate))
            throw new ArgumentException("Consent dates are out of order.", nameof(f));
    }

    public static void EnsureValid(ConditionFields f)
    {
        if (f.Description.Length == 0)
            throw new ArgumentException("Condition description is required.", nameof(f));
    }

    /// <summary>Done if completed; None if the consent is inactive or there is no due date; else by window.</summary>
    public static DueState ConditionState(Condition condition, bool consentActive, DateOnly today)
    {
        if (condition.CompletedOn is not null)
            return DueState.Done;
        if (!consentActive || condition.Fields.DueDate is not { } due)
            return DueState.None;
        return ByWindow(due, today, ConditionWarningDays);
    }

    /// <summary>Only Granted consents with an expiry date are tracked.</summary>
    public static DueState ExpiryState(Consent consent, DateOnly today) =>
        consent.Fields.Status == ConsentStatus.Granted && consent.Fields.ExpiryDate is { } expiry
            ? ByWindow(expiry, today, ExpiryWarningDays)
            : DueState.None;

    /// <summary>Overdue beats DueSoon; anything else is None (attention only cares about these two).</summary>
    public static DueState Worst(IEnumerable<DueState> states)
    {
        var list = states as ICollection<DueState> ?? states.ToList();
        if (list.Contains(DueState.Overdue))
            return DueState.Overdue;
        return list.Contains(DueState.DueSoon) ? DueState.DueSoon : DueState.None;
    }

    private static DueState ByWindow(DateOnly date, DateOnly today, int warningDays) =>
        date < today ? DueState.Overdue
        : date <= today.AddDays(warningDays) ? DueState.DueSoon
        : DueState.Upcoming;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
