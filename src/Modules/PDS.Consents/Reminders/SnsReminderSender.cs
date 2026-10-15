using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Options;

namespace PDS.Consents.Reminders;

internal sealed class SnsReminderSender(IAmazonSimpleNotificationService sns, IOptions<ConsentRemindersOptions> options)
    : IReminderSender
{
    public async Task<string> PublishAsync(string subject, string body, CancellationToken ct)
    {
        var response = await sns.PublishAsync(
            new PublishRequest { TopicArn = options.Value.TopicArn, Subject = subject, Message = body }, ct);
        return response.MessageId;
    }
}
