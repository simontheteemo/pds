using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PDS.Portfolio.Data;
using PDS.Portfolio.Features;
using PDS.Shared.Data;
using PDS.Shared.Security;

namespace PDS.Portfolio;

public static class PortfolioModule
{
    /// <summary>Tables this module owns. The host creates them locally; CDK creates them in AWS.</summary>
    public static IReadOnlyList<TableDefinition> Tables { get; } = [PortfolioTable.Definition];

    public static IServiceCollection AddPortfolioModule(this IServiceCollection services)
    {
        services.AddSingleton(PortfolioTable.Definition);
        services.AddSingleton<ProjectStore>();
        services.AddValidatorsFromAssemblyContaining<CreateProjectValidator>(includeInternalTypes: true);
        return services;
    }

    public static IEndpointRouteBuilder MapPortfolioEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/portfolio").WithTags("Portfolio").RequireAuthorization(Policies.CanRead);
        ListProjects.Map(group);
        CreateProject.Map(group);
        GetProject.Map(group);
        UpdateProject.Map(group);
        return app;
    }
}
