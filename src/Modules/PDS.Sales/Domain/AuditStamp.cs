namespace PDS.Sales.Domain;

internal readonly record struct AuditStamp(DateTimeOffset At, string By);
