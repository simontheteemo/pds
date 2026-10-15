using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Consents.Features;
using PDS.Shared.Settings;

namespace PDS.Consents.Reminders;

internal static class PreviewReminders
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/preview", Handle).WithName("PreviewConsentReminders");

    internal static async Task<Ok<ReminderPreview>> Handle(
        DueItemsQuery query, IOptions<LocaleOptions> locale, IOptions<ConsentRemindersOptions> options, CancellationToken ct)
    {
        var (today, items) = await query.LoadAsync(ct);
        var digest = ConsentDigest.Build(today, items, CultureInfo.GetCultureInfo(locale.Value.Culture), options.Value.AppUrl);
        return TypedResults.Ok(new ReminderPreview(
            today, items.Count,
            digest?.OverdueCount ?? 0, digest?.DueSoonCount ?? 0,
            digest?.Subject, digest?.Body, options.Value.IsConfigured));
    }
}
