namespace PDS.Reporting.Contracts;

/// <summary>
/// A portfolio-wide table a module exports from its own data. Register with
/// <c>services.AddScoped&lt;IExportDataset, MyDataset&gt;()</c> in the module's Add method
/// (datasets may depend on scoped services such as <c>IPortfolioQueries</c>).
/// </summary>
public interface IExportDataset
{
    /// <summary>Stable kebab-case id: URL segment and file name (later the S3 prefix and Athena table).</summary>
    string Name { get; }

    string Title { get; }

    string Description { get; }

    Task<ExportTable> LoadAsync(CancellationToken ct);
}
