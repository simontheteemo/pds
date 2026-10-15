namespace PDS.Consents.Domain;

/// <summary>Display labels for emails; keep in step with web/src/features/consents/types.ts.</summary>
internal static class ConsentLabels
{
    public static string Type(ConsentType type) => type switch
    {
        ConsentType.ResourceConsent => "Resource consent",
        ConsentType.SubdivisionConsent => "Subdivision consent",
        ConsentType.BuildingConsent => "Building consent",
        ConsentType.Section224c => "s224(c) certificate",
        ConsentType.CodeComplianceCertificate => "Code compliance certificate (CCC)",
        _ => "Other",
    };
}
