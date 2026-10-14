using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PDS.Costs.Data;
using PDS.Costs.Features;
using PDS.Shared.Data;
using PDS.Shared.Security;

namespace PDS.Costs;

public static class CostsModule
{
    /// <summary>Tables this module owns. The host creates them locally; CDK creates them in AWS.</summary>
    public static IReadOnlyList<TableDefinition> Tables { get; } = [CostsTable.Definition];

    public static IServiceCollection AddCostsModule(this IServiceCollection services)
    {
        services.AddSingleton(CostsTable.Definition);
        services.AddSingleton<CostStore>();
        services.AddOptions<CostsOptions>().BindConfiguration(CostsOptions.Section);
        services.AddValidatorsFromAssemblyContaining<CreateBudgetLineValidator>(includeInternalTypes: true);
        return services;
    }

    public static IEndpointRouteBuilder MapCostsEndpoints(this IEndpointRouteBuilder app)
    {
        var costs = app.MapGroup("/api/costs/projects/{projectId:guid}")
            .WithTags("Costs")
            .RequireAuthorization(Policies.CanRead);
        CreateBudgetLine.Map(costs);
        AddStandardLines.Map(costs);
        UpdateBudgetLine.Map(costs);
        DeleteBudgetLine.Map(costs);
        return app;
    }
}
