namespace PDS.Consents.Domain;

internal readonly record struct AuditStamp(DateTimeOffset At, string By);
