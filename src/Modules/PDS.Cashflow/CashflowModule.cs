using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PDS.Cashflow.Features;
using PDS.Shared.Security;

namespace PDS.Cashflow;

public static class CashflowModule
{
    /// <summary>No-op: the queries this module consumes are registered by their owning modules.</summary>
    public static IServiceCollection AddCashflowModule(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapCashflowEndpoints(this IEndpointRouteBuilder app)
    {
        var cashflow = app.MapGroup("/api/cashflow/projects/{projectId:guid}")
            .WithTags("Cashflow")
            .RequireAuthorization(Policies.CanRead);
        GetProjectCashflow.Map(cashflow);
        return app;
    }
}
