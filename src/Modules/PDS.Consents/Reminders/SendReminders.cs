using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PDS.Consents.Features;
using PDS.Shared.Settings;

namespace PDS.Consents.Reminders;

internal static partial class SendReminders
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/send", Handle).WithName("SendConsentReminders");

    internal static async Task<Ok<ReminderSendResult>> Handle(
        IServiceProvider services, DueItemsQuery query, IOptions<LocaleOptions> locale,
        IOptions<ConsentRemindersOptions> options, ILoggerFactory loggers, CancellationToken ct)
    {
        var log = loggers.CreateLogger("PDS.Consents.Reminders");
        try
        {
            if (!options.Value.IsConfigured)
                return Done(log, new ReminderSendResult(ReminderSendOutcome.NotConfigured, 0, null), 0, 0);

            var (today, items) = await query.LoadAsync(ct);
            var digest = ConsentDigest.Build(today, items, CultureInfo.GetCultureInfo(locale.Value.Culture), options.Value.AppUrl);
            if (digest is null)
                return Done(log, new ReminderSendResult(ReminderSendOutcome.NothingDue, 0, null), 0, 0);

            // Resolved here, not injected, so the SNS client is never built unless we actually send.
            var sender = services.GetRequiredService<IReminderSender>();
            var messageId = await sender.PublishAsync(digest.Subject, digest.Body, ct);
            return Done(log, new ReminderSendResult(ReminderSendOutcome.Sent, digest.ItemCount, messageId),
                digest.OverdueCount, digest.DueSoonCount);
        }
        catch (Exception ex)
        {
            SendFailed(log, ex);
            throw;
        }
    }

    private static Ok<ReminderSendResult> Done(ILogger log, ReminderSendResult result, int overdue, int dueSoon)
    {
        Completed(log, result.Outcome, result.ItemCount, overdue, dueSoon);
        return TypedResults.Ok(result);
    }

    [LoggerMessage(EventId = 4100, EventName = "ConsentReminderSendCompleted", Level = LogLevel.Information,
        Message = "Consent reminder digest: {Outcome} ({ItemCount} items: {OverdueCount} overdue, {DueSoonCount} due soon)")]
    private static partial void Completed(
        ILogger log, ReminderSendOutcome outcome, int itemCount, int overdueCount, int dueSoonCount);

    /// <summary>The CDK metric filter alarms on this event name; don't rename it.</summary>
    [LoggerMessage(EventId = 4101, EventName = "ConsentReminderSendFailed", Level = LogLevel.Error,
        Message = "Consent reminder digest failed to send")]
    private static partial void SendFailed(ILogger log, Exception ex);
}
