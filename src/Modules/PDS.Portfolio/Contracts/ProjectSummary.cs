namespace PDS.Portfolio.Contracts;

public sealed record ProjectSummary(ProjectId Id, string Code, string Name, string Stage, string Status, bool IsArchived);
