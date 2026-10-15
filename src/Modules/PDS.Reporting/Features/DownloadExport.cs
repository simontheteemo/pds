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

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{name}", Handle)
            .WithName("DownloadExport")
            .Produces(StatusCodes.Status200OK, typeof(string), "text/csv");

    internal static async Task<Results<FileContentHttpResult, NotFound>> Handle(
        string name, ExportCatalog catalog, ICurrentUser user, TimeProvider clock, IOptions<LocaleOptions> locale,
        ILoggerFactory loggers, HttpContext http, CancellationToken ct)
    {
        if (catalog.Find(name) is not { } dataset)
            return TypedResults.NotFound();

        var table = await dataset.LoadAsync(ct);
        var bytes = CsvWriter.Write(table);
        loggers.CreateLogger("PDS.Reporting.Export").LogInformation(
            "Export {Dataset} downloaded by {UserName}: {Rows} rows", dataset.Name, user.Name, table.Rows.Count);

        var today = LocalDate.Today(clock, locale.Value);
        http.Response.Headers.CacheControl = "no-store";
        return TypedResults.File(
            bytes, CsvContentType, $"pds-{dataset.Name}-{today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.csv");
    }
}
