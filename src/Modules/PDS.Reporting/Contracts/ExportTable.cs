namespace PDS.Reporting.Contracts;

/// <summary>One output column: a snake_case name and a typed value (null, string, bool, number, DateOnly, DateTimeOffset, Guid or enum).</summary>
public sealed record ExportColumn<T>(string Name, Func<T, object?> Value);

public sealed class ExportTable
{
    private ExportTable(IReadOnlyList<string> columns, IReadOnlyList<object?[]> rows)
    {
        Columns = columns;
        Rows = rows;
    }

    public IReadOnlyList<string> Columns { get; }

    public IReadOnlyList<object?[]> Rows { get; }

    public static ExportTable From<T>(IEnumerable<T> rows, IReadOnlyList<ExportColumn<T>> columns) =>
        new(
            columns.Select(c => c.Name).ToList(),
            rows.Select(r => columns.Select(c => c.Value(r)).ToArray()).ToList());
}
