namespace PDS.Documents.Storage;

internal enum StorageMode
{
    S3,
    Local,
}

/// <summary>Bound from the "Documents" configuration section.</summary>
internal sealed class DocumentsOptions
{
    public const string Section = "Documents";

    public static readonly IReadOnlyList<string> DefaultCategories =
        ["Contract", "Consent", "Drawing", "Report", "Invoice", "Photo", "Correspondence", "Other"];

    public StorageMode Storage { get; set; } = StorageMode.S3;

    /// <summary>S3 bucket for files (set by CDK as Documents__BucketName).</summary>
    public string? BucketName { get; set; }

    /// <summary>Local mode only. Defaults to &lt;temp&gt;/pds-documents.</summary>
    public string? LocalPath { get; set; }

    public long MaxFileSizeBytes { get; set; } = 100L * 1024 * 1024;

    /// <summary>Empty means the defaults. (Binding appends to a non-empty initial list, so the default lives elsewhere.)</summary>
    public List<string> Categories { get; set; } = [];

    public IReadOnlyList<string> EffectiveCategories =>
        Categories.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            is { Count: > 0 } configured ? configured : DefaultCategories;
}
