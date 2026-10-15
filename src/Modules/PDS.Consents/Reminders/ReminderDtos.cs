namespace PDS.Consents.Reminders;

internal sealed record ReminderPreview(
    DateOnly Today, int ItemCount, int OverdueCount, int DueSoonCount, string? Subject, string? Body, bool DeliveryConfigured);

internal enum ReminderSendOutcome { Sent, NothingDue, NotConfigured }

internal sealed record ReminderSendResult(ReminderSendOutcome Outcome, int ItemCount, string? MessageId);
