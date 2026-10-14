namespace PDS.Contractors.Domain;

internal static class Compliance
{
    public const int WarningDays = 30;

    /// <summary>Expired before today; ExpiringSoon from today to today+30 inclusive; otherwise Current.</summary>
    public static ComplianceStatus For(DateOnly? expiry, DateOnly today) => expiry switch
    {
        null => ComplianceStatus.NotRecorded,
        { } d when d < today => ComplianceStatus.Expired,
        { } d when d <= today.AddDays(WarningDays) => ComplianceStatus.ExpiringSoon,
        _ => ComplianceStatus.Current,
    };

    /// <summary>The worst status among the recorded dates; NotRecorded when none are recorded.</summary>
    public static ComplianceStatus Overall(IEnumerable<DateOnly?> expiries, DateOnly today)
    {
        var statuses = expiries.Where(d => d is not null).Select(d => For(d, today)).ToList();
        if (statuses.Count == 0)
            return ComplianceStatus.NotRecorded;
        if (statuses.Contains(ComplianceStatus.Expired))
            return ComplianceStatus.Expired;
        return statuses.Contains(ComplianceStatus.ExpiringSoon) ? ComplianceStatus.ExpiringSoon : ComplianceStatus.Current;
    }
}
