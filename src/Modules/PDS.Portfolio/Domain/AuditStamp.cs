namespace PDS.Portfolio.Domain;

internal readonly record struct AuditStamp(DateTimeOffset At, string By);
