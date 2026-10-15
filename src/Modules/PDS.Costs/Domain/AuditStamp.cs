namespace PDS.Costs.Domain;

internal readonly record struct AuditStamp(DateTimeOffset At, string By);
