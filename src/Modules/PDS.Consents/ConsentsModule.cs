using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PDS.Consents.Data;
using PDS.Consents.Features;
using PDS.Shared.Data;
using PDS.Shared.Security;

namespace PDS.Consents;

public static class ConsentsModule
{
    /// <summary>Tables this module owns. The host creates them locally; CDK creates them in AWS.</summary>
    public static IReadOnlyList<TableDefinition> Tables { get; } = [ConsentsTable.Definition];

    public static IServiceCollection AddConsentsModule(this IServiceCollection services)
    {
        services.AddSingleton(ConsentsTable.Definition);
        services.AddSingleton<ConsentStore>();
        services.AddValidatorsFromAssemblyContaining<CreateConsentValidator>(includeInternalTypes: true);
        return services;
    }

    public static IEndpointRouteBuilder MapConsentsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/consents/projects/{projectId:guid}/consents")
            .WithTags("Consents")
            .RequireAuthorization(Policies.CanRead);
        ListConsents.Map(group);
        GetConsent.Map(group);
        CreateConsent.Map(group);
        UpdateConsent.Map(group);
        return app;
    }
}
