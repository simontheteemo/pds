using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;

namespace PDS.Documents.Storage;

/// <summary>Development-only stand-ins for S3 presigned PUT/GET. Mapped only when Documents:Storage=Local.</summary>
internal static class LocalFileEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(LocalFileStorage.RoutePrefix).AllowAnonymous().ExcludeFromDescription();
        group.MapPut("/{**key}", Put);
        group.MapGet("/{**key}", Get);
    }

    private static async Task<IResult> Put(
        string key, long expires, string? sig, HttpContext context, LocalFileStorage storage, TimeProvider clock)
    {
        var contentType = context.Request.ContentType ?? string.Empty;
        if (!storage.Verify("PUT", key, expires, contentType, string.Empty, sig, clock.GetUtcNow()) || storage.PathFor(key) is not { } path)
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        // S3 accepts up to 5 GB per PUT; lift Kestrel's 30 MB default so local behaves the same.
        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            limit.MaxRequestBodySize = null;
        await storage.WriteAsync(path, context.Request.Body, contentType, context.RequestAborted);
        return Results.Ok();
    }

    private static IResult Get(
        string key, long expires, string? type, string? name, string? sig, LocalFileStorage storage, TimeProvider clock)
    {
        if (type is null || name is null
            || !storage.Verify("GET", key, expires, type, name, sig, clock.GetUtcNow())
            || storage.PathFor(key) is not { } path || !File.Exists(path))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        return Results.File(path, type, name);
    }
}
