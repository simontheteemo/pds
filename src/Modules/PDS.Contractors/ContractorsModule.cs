using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PDS.Contractors.Data;
using PDS.Contractors.Features;
using PDS.Shared.Data;
using PDS.Shared.Security;

namespace PDS.Contractors;

public static class ContractorsModule
{
    /// <summary>Tables this module owns. The host creates them locally; CDK creates them in AWS.</summary>
    public static IReadOnlyList<TableDefinition> Tables { get; } = [ContractorsTable.Definition];

    public static IServiceCollection AddContractorsModule(this IServiceCollection services)
    {
        services.AddSingleton(ContractorsTable.Definition);
        services.AddSingleton<ContractorStore>();
        services.AddValidatorsFromAssemblyContaining<CreateContractorValidator>(includeInternalTypes: true);
        return services;
    }

    public static IEndpointRouteBuilder MapContractorsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/contractors")
            .WithTags("Contractors")
            .RequireAuthorization(Policies.CanRead);
        ListContractors.Map(group);
        GetContractor.Map(group);
        CreateContractor.Map(group);
        UpdateContractor.Map(group);
        SetContractorActive.Map(group);
        return app;
    }
}
