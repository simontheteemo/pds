namespace PDS.Actions.Domain;

/// <summary>
/// Which module a linked record belongs to. A typed reference only: existence is not checked.
/// Not named LinkedModule so its OpenAPI schema does not collide with the Documents enum of that name.
/// </summary>
internal enum ActionLinkedModule
{
    Risk,
    Consent,
    Contract,
    Milestone,
    Meeting,
}
