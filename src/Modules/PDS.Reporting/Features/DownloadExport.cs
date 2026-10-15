using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PDS.Shared.Security;
using PDS.Shared.Settings;

namespace PDS.Reporting.Features;

internal static class DownloadExport
{
    public const string CsvContentType = "text/csv; charset=utf-8";

    // API Gateway/Lambda buffered response limit is 6 MB including JSON-envelope escaping, so headroom is kept.
    internal const int MaxExportBytes = 5_500_000;

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{name}", Handle)
            .WithName("DownloadExport")
            .Produces(StatusCodes.Status200OK, typeof(string), "text/csv")
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge);

    internal static async Task<Results<FileContentHttpResult, NotFound, ProblemHttpResult>> Handle(
        string name, ExportCatalog catalog, ICurrentUser user, TimeProvider clock, IOptions<LocaleOptions> locale,
        ILoggerFactory loggers, HttpContext http, CancellationToken ct)
    {
        if (catalog.Find(name) is not { } dataset)
            return TypedResults.NotFound();

        var table = await dataset.LoadAsync(ct);
        var bytes = CsvWriter.Write(table);
        if (bytes.Length > MaxExportBytes)
        {
            loggers.CreateLogger("PDS.Reporting.Export").LogWarning(
                "Export {Dataset} refused as too large for {UserName}: {Bytes} bytes, {Rows} rows",
                dataset.Name, user.Name, bytes.Length, table.Rows.Count);
            return TypedResults.Problem(
                title: "This export is too large to download.",
                detail: "The file would be larger than the download limit. Contact an administrator to get this data.",
                statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        loggers.CreateLogger("PDS.Reporting.Export").LogInformation(
            "Export {Dataset} downloaded by {UserName}: {Rows} rows", dataset.Name, user.Name, table.Rows.Count);

        var today = LocalDate.Today(clock, locale.Value);
        http.Response.Headers.CacheControl = "no-store";
        return TypedResults.File(
            bytes, CsvContentType, $"pds-{dataset.Name}-{today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.csv");
    }
}
