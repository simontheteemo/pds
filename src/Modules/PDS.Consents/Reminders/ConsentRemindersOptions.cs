namespace PDS.Consents.Reminders;

/// <summary>Set by CDK when the deployment enables consent reminders; absent locally.</summary>
internal sealed class ConsentRemindersOptions
{
    public const string Section = "ConsentReminders";

    /// <summary>SNS topic the digest is published to. Unset means reminders are not configured.</summary>
    public string? TopicArn { get; set; }

    /// <summary>Site base URL, used for the "open the register" link.</summary>
    public string? AppUrl { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(TopicArn);
}
