namespace PDS.Risks.Domain;

internal readonly record struct AuditStamp(DateTimeOffset At, string By);
