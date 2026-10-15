namespace PDS.Documents.Storage;

/// <summary>Extension allowlist. The server picks the content type; the browser's guess is never used.</summary>
internal static class FileTypes
{
    private static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pdf"] = "application/pdf",
        ["jpg"] = "image/jpeg",
        ["jpeg"] = "image/jpeg",
        ["png"] = "image/png",
        ["gif"] = "image/gif",
        ["webp"] = "image/webp",
        ["heic"] = "image/heic",
        ["tif"] = "image/tiff",
        ["tiff"] = "image/tiff",
        ["doc"] = "application/msword",
        ["docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ["xls"] = "application/vnd.ms-excel",
        ["xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ["ppt"] = "application/vnd.ms-powerpoint",
        ["pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ["csv"] = "text/csv",
        ["txt"] = "text/plain",
        ["rtf"] = "application/rtf",
        ["msg"] = "application/vnd.ms-outlook",
        ["eml"] = "message/rfc822",
        ["dwg"] = "image/vnd.dwg",
        ["dxf"] = "image/vnd.dxf",
        ["zip"] = "application/zip",
    };

    public static IReadOnlyList<string> Extensions { get; } = ByExtension.Keys.Order(StringComparer.Ordinal).ToList();

    /// <summary>The final path segment, trimmed. Handles both / and \ (browsers may send C:\fakepath\x.pdf).</summary>
    public static string CleanFileName(string? fileName) =>
        Path.GetFileName((fileName ?? string.Empty).Replace('\\', '/')).Trim();

    /// <returns>The canonical content type, or null when the extension is not allowed.</returns>
    public static string? ContentTypeFor(string? fileName)
    {
        var extension = Path.GetExtension(CleanFileName(fileName)).TrimStart('.');
        return extension.Length > 0 && ByExtension.TryGetValue(extension, out var type) ? type : null;
    }

    public static string AllowedList => string.Join(", ", Extensions);
}
