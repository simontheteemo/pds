using System.Globalization;
using System.Text;
using PDS.Reporting.Contracts;

namespace PDS.Reporting.Features;

/// <summary>RFC 4180 CSV, UTF-8 with BOM (so Excel detects the encoding), CRLF line endings, invariant formats.</summary>
internal static class CsvWriter
{
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    public static byte[] Write(ExportTable table)
    {
        var sb = new StringBuilder();
        AppendRow(sb, table.Columns.Select(c => (object?)c));
        foreach (var row in table.Rows)
            AppendRow(sb, row);

        var preamble = Utf8WithBom.GetPreamble();
        var body = Utf8WithBom.GetBytes(sb.ToString());
        var bytes = new byte[preamble.Length + body.Length];
        preamble.CopyTo(bytes, 0);
        body.CopyTo(bytes, preamble.Length);
        return bytes;
    }

    private static void AppendRow(StringBuilder sb, IEnumerable<object?> values)
    {
        var first = true;
        foreach (var value in values)
        {
            if (!first)
                sb.Append(',');
            first = false;
            sb.Append(Field(value));
        }

        sb.Append("\r\n");
    }

    internal static string Field(object? value) => value switch
    {
        null => string.Empty,
        string s => Quote(GuardFormula(s)),
        bool b => b ? "true" : "false",
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTimeOffset t => t.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
        Enum e => e.ToString(),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => Quote(GuardFormula(value.ToString() ?? string.Empty)),
    };

    /// <summary>Stops spreadsheets evaluating user text as a formula (OWASP CSV injection). Numbers never reach here.</summary>
    private static string GuardFormula(string s) =>
        s.Length > 0 && s[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + s : s;

    private static string Quote(string s) =>
        s.AsSpan().IndexOfAny(",\"\r\n") >= 0 ? "\"" + s.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : s;
}
