using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PDS.Costs.Data;
using PDS.Costs.Features;
using PDS.Reporting.Contracts;
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
        services.AddScoped<IExportDataset, CostLinesExport>();
        services.AddScoped<IExportDataset, CostEntriesExport>();
        services.AddOptions<CostsOptions>().BindConfiguration(CostsOptions.Section);
        services.AddValidatorsFromAssemblyContaining<CreateBudgetLineValidator>(includeInternalTypes: true);
        return services;
    }

    public static IEndpointRouteBuilder MapCostsEndpoints(this IEndpointRouteBuilder app)
    {
        var costs = app.MapGroup("/api/costs/projects/{projectId:guid}")
            .WithTags("Costs")
            .RequireAuthorization(Policies.CanRead);
        GetCostReport.Map(costs);
        CreateBudgetLine.Map(costs);
        AddStandardLines.Map(costs);
        UpdateBudgetLine.Map(costs);
        DeleteBudgetLine.Map(costs);
        CreateCommitment.Map(costs);
        UpdateCommitment.Map(costs);
        DeleteCommitment.Map(costs);
        CreateActualCost.Map(costs);
        UpdateActualCost.Map(costs);
        DeleteActualCost.Map(costs);
        return app;
    }
}
