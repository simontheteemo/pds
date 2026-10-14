using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PDS.Sales.Data;
using PDS.Sales.Features;
using PDS.Shared.Data;
using PDS.Shared.Security;

namespace PDS.Sales;

public static class SalesModule
{
    /// <summary>Tables this module owns. The host creates them locally; CDK creates them in AWS.</summary>
    public static IReadOnlyList<TableDefinition> Tables { get; } = [SalesTable.Definition];

    public static IServiceCollection AddSalesModule(this IServiceCollection services)
    {
        services.AddSingleton(SalesTable.Definition);
        services.AddSingleton<UnitStore>();
        services.AddValidatorsFromAssemblyContaining<CreateUnitValidator>(includeInternalTypes: true);
        return services;
    }

    public static IEndpointRouteBuilder MapSalesEndpoints(this IEndpointRouteBuilder app)
    {
        var units = app.MapGroup("/api/sales/projects/{projectId:guid}/units")
            .WithTags("Sales")
            .RequireAuthorization(Policies.CanRead);
        ListUnits.Map(units);
        GetUnit.Map(units);
        CreateUnit.Map(units);
        UpdateUnit.Map(units);
        UpdateSale.Map(units);
        var sales = app.MapGroup("/api/sales")
            .WithTags("Sales")
            .RequireAuthorization(Policies.CanRead);
        ListSalesSummaries.Map(sales);
        return app;
    }
}
