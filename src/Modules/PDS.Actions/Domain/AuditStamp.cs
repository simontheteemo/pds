namespace PDS.Actions.Domain;

internal readonly record struct AuditStamp(DateTimeOffset At, string By);
