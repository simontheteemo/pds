namespace PDS.Documents.Domain;

/// <summary>Soft link to another module's record, with a label snapshot for display.</summary>
internal sealed record DocumentLink(LinkedModule? Module, Guid RecordId, string Label);
