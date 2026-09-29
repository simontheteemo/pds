using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PDS.Risks.Data;
using PDS.Risks.Features;
using PDS.Shared.Data;
using PDS.Shared.Security;

namespace PDS.Risks;

public static class RisksModule
{
    /// <summary>Tables this module owns. The host creates them locally; CDK creates them in AWS.</summary>
    public static IReadOnlyList<TableDefinition> Tables { get; } = [RisksTable.Definition];

    public static IServiceCollection AddRisksModule(this IServiceCollection services)
    {
        services.AddSingleton(RisksTable.Definition);
        services.AddSingleton<RiskStore>();
        services.AddValidatorsFromAssemblyContaining<CreateRiskValidator>(includeInternalTypes: true);
        return services;
    }

    public static IEndpointRouteBuilder MapRisksEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/risks/projects/{projectId:guid}/risks")
            .WithTags("Risks")
            .RequireAuthorization(Policies.CanRead);
        ListRisks.Map(group);
        GetRisk.Map(group);
        CreateRisk.Map(group);
        UpdateRisk.Map(group);
        CloseRisk.Map(group);
        return app;
    }
}
