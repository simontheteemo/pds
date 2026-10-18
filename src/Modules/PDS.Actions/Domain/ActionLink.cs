namespace PDS.Actions.Domain;

/// <summary>Optional reference to the record an action came from. Module is nullable so a missing module is a 400.</summary>
internal sealed record ActionLink(ActionLinkedModule? Module, Guid RecordId, string Label);
