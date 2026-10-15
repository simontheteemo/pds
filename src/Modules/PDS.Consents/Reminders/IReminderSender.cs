namespace PDS.Consents.Reminders;

internal interface IReminderSender
{
    /// <summary>Publishes the digest; returns the provider's message id.</summary>
    Task<string> PublishAsync(string subject, string body, CancellationToken ct);
}
