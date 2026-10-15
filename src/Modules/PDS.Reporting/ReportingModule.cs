using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PDS.Reporting.Features;
using PDS.Shared.Security;

namespace PDS.Reporting;

public static class ReportingPolicies
{
    /// <summary>Bulk export of commercial data: Managers and Admins only.</summary>
    public const string CanExport = "CanExport";
}

public static class ReportingModule
{
    public static IServiceCollection AddReportingModule(this IServiceCollection services)
    {
        services.AddScoped<ExportCatalog>();
        services.AddAuthorizationBuilder()
            .AddPolicy(ReportingPolicies.CanExport, p => p.RequireRole(Roles.Manager, Roles.Admin));
        return services;
    }

    public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reporting/exports")
            .WithTags("Reporting")
            .RequireAuthorization(ReportingPolicies.CanExport);
        ListExports.Map(group);
        DownloadExport.Map(group);
        return app;
    }
}
