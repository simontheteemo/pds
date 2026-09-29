using System.Text.Json.Serialization;
using Amazon.Lambda.AspNetCoreServer.Hosting;
using PDS.Api;
using PDS.Api.Security;
using PDS.Portfolio;
using PDS.Shared.Data;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog(logger =>
{
    logger.MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .Enrich.FromLogContext();
    if (builder.Environment.IsDevelopment())
        logger.WriteTo.Console();
    else
        logger.WriteTo.Console(new CompactJsonFormatter());
});
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);
builder.Services.ConfigureHttpJsonOptions(o =>
{
    // Strict numbers keep the OpenAPI types plain (number, not number | string) and reject "12" for 12.
    o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
});
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["correlationId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddPdsSettings(builder.Configuration);
builder.Services.AddPdsSecurity(builder.Configuration);
builder.Services.AddDynamo();
builder.Services.AddPortfolioModule();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging(o => o.EnrichDiagnosticContext = (diagnostics, http) =>
    diagnostics.Set("UserName", http.User.Identity?.Name ?? "anonymous"));
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = ex => ex is BadHttpRequestException bad ? bad.StatusCode : StatusCodes.Status500InternalServerError,
});
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapHostEndpoints();
app.MapPortfolioEndpoints();

await app.RunAsync();

public partial class Program;
