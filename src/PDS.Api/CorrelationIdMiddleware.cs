using Serilog.Context;

namespace PDS.Api;

/// <summary>Accepts a safe incoming X-Correlation-Id or generates one; echoes it and adds it to every log line.</summary>
internal sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string Header = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[Header].ToString();
        var id = IsValid(incoming) ? incoming : Guid.NewGuid().ToString("N");
        context.TraceIdentifier = id;
        context.Response.Headers[Header] = id;

        using (LogContext.PushProperty("CorrelationId", id))
        {
            await next(context);
        }
    }

    internal static bool IsValid(string value) =>
        value.Length is > 0 and <= 64 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
