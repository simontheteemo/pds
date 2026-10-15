using System.Text;

namespace PDS.Documents.Storage;

internal static class ContentDispositionHeader
{
    /// <summary>attachment; filename="ascii-fallback"; filename*=UTF-8''percent-encoded (RFC 6266).</summary>
    public static string Attachment(string fileName)
    {
        var ascii = new StringBuilder(fileName.Length);
        foreach (var c in fileName)
            ascii.Append(c is >= ' ' and <= '~' and not '"' and not '\\' ? c : '_');
        return $"attachment; filename=\"{ascii}\"; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
    }
}
