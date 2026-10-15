using System.Globalization;
using System.Text;
using PDS.Consents.Domain;
using PDS.Consents.Features;

namespace PDS.Consents.Reminders;

internal sealed record Digest(string Subject, string Body, int ItemCount, int OverdueCount, int DueSoonCount);

/// <summary>Plain-text email digest of the due list (SNS email: subject ≤ 100 ASCII chars, body ≤ 256 KB).</summary>
internal static class ConsentDigest
{
    public const int MaxItems = 100;
    public const int MaxDescriptionLength = 200;

    public static Digest? Build(DateOnly today, IReadOnlyList<DueItem> items, CultureInfo culture, string? appUrl)
    {
        if (items.Count == 0)
            return null;

        var overdue = items.Where(i => i.State == DueState.Overdue).ToList();
        var dueSoon = items.Where(i => i.State == DueState.DueSoon).ToList();
        var counts = string.Join(", ", new[]
        {
            overdue.Count > 0 ? $"{overdue.Count} overdue" : null,
            dueSoon.Count > 0 ? $"{dueSoon.Count} due soon" : null,
        }.OfType<string>());

        var body = new StringBuilder();
        body.Append("Consents due — ").AppendLine(today.ToString("dddd d MMMM yyyy", culture)).AppendLine();
        body.Append(counts).AppendLine(".");
        body.Append("(Conditions due within ").Append(ConsentRules.ConditionWarningDays)
            .Append(" days; consent expiries within ").Append(ConsentRules.ExpiryWarningDays).AppendLine(" days.)");

        var remaining = MaxItems;
        AppendSection(body, "OVERDUE", overdue, culture, ref remaining);
        AppendSection(body, "DUE SOON", dueSoon, culture, ref remaining);
        if (items.Count > MaxItems)
            body.AppendLine().Append("…and ").Append(items.Count - MaxItems).AppendLine(" more. Open the register to see them all.");

        if (!string.IsNullOrWhiteSpace(appUrl))
            body.AppendLine().Append("Open the consents register: ").Append(appUrl.TrimEnd('/')).AppendLine("/consents");

        return new Digest($"Consents due: {counts}", body.ToString(), items.Count, overdue.Count, dueSoon.Count);
    }

    private static void AppendSection(
        StringBuilder body, string heading, List<DueItem> items, CultureInfo culture, ref int remaining)
    {
        if (items.Count == 0 || remaining <= 0)
            return;
        body.AppendLine().AppendLine(heading);
        foreach (var item in items.Take(remaining))
        {
            body.Append("- ").Append(item.Date.ToString("d MMM yyyy", culture))
                .Append(" · ").Append(item.ProjectCode).Append(' ').AppendLine(OneLine(item.ProjectName));
            body.Append("  ").Append(ConsentLabels.Type(item.ConsentType)).Append(": ").Append(OneLine(item.ConsentTitle));
            if (item.ConsentReference is { } reference)
                body.Append(" (").Append(OneLine(reference)).Append(')');
            body.AppendLine();
            body.Append("  ");
            if (item.Kind == DueItemKind.Expiry)
                body.AppendLine(item.State == DueState.Overdue ? "Consent expired" : "Consent expires");
            else
            {
                if (item.ConditionReference is { } condRef)
                    body.Append(OneLine(condRef)).Append(": ");
                body.AppendLine(OneLine(item.Description));
            }
        }
        remaining -= Math.Min(items.Count, remaining);
    }

    /// <summary>Collapses whitespace (including line breaks) and truncates to the description limit.</summary>
    private static string OneLine(string value)
    {
        var collapsed = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length <= MaxDescriptionLength ? collapsed : collapsed[..(MaxDescriptionLength - 1)] + "…";
    }
}
