using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PDS.Actions.Data;
using PDS.Actions.Features;
using PDS.Shared.Data;
using PDS.Shared.Security;

namespace PDS.Actions;

public static class ActionsModule
{
    /// <summary>Tables this module owns. The host creates them locally; CDK creates them in AWS.</summary>
    public static IReadOnlyList<TableDefinition> Tables { get; } = [ActionsTable.Definition];

    public static IServiceCollection AddActionsModule(this IServiceCollection services)
    {
        services.AddSingleton(ActionsTable.Definition);
        services.AddSingleton<ActionStore>();
        services.AddValidatorsFromAssemblyContaining<CreateActionValidator>(includeInternalTypes: true);
        return services;
    }

    public static IEndpointRouteBuilder MapActionsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/actions/projects/{projectId:guid}/actions")
            .WithTags("Actions")
            .RequireAuthorization(Policies.CanRead);
        ListProjectActions.Map(group);
        GetAction.Map(group);
        CreateAction.Map(group);
        UpdateAction.Map(group);
        ChangeActionStatus.Map(group);
        return app;
    }
}
