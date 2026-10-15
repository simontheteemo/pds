namespace PDS.Documents.Domain;

internal readonly record struct AuditStamp(DateTimeOffset At, string By);
