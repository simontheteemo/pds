using System.Text.RegularExpressions;
using PDS.Reporting.Contracts;

namespace PDS.Reporting.Features;

/// <summary>Every registered dataset, validated when built (scoped, because datasets may use scoped services).</summary>
internal sealed partial class ExportCatalog
{
    private readonly Dictionary<string, IExportDataset> byName;

    public ExportCatalog(IEnumerable<IExportDataset> datasets)
    {
        byName = new Dictionary<string, IExportDataset>(StringComparer.Ordinal);
        foreach (var d in datasets)
        {
            if (!NamePattern().IsMatch(d.Name))
                throw new InvalidOperationException($"Export dataset name '{d.Name}' must be kebab-case.");
            if (!byName.TryAdd(d.Name, d))
                throw new InvalidOperationException($"Export dataset '{d.Name}' is registered twice.");
        }

        All = byName.Values.OrderBy(d => d.Title, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public IReadOnlyList<IExportDataset> All { get; }

    public IExportDataset? Find(string name) => byName.GetValueOrDefault(name);

    [GeneratedRegex("^[a-z][a-z0-9-]*$")]
    private static partial Regex NamePattern();
}
