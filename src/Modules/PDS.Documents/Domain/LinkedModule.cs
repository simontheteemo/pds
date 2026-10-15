namespace PDS.Documents.Domain;

/// <summary>Which module a linked record belongs to. A typed reference only: existence is not checked (spec §1).</summary>
internal enum LinkedModule
{
    Consent,
    Contract,
    Risk,
}
