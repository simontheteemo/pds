using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PDS.Shared.Security;

namespace PDS.Api.Security;

internal static class SecurityServiceCollectionExtensions
{
    public static IServiceCollection AddPdsSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.Section))
            .Validate<IHostEnvironment>(
                (options, env) => options.Mode != AuthMode.Development || env.IsDevelopment(),
                "Auth:Mode=Development is only allowed when ASPNETCORE_ENVIRONMENT is Development.")
            .ValidateOnStart();

        services.AddAuthentication(SchemeNames.Selector)
            .AddPolicyScheme(SchemeNames.Selector, null, o => o.ForwardDefaultSelector = context =>
                context.RequestServices.GetRequiredService<IOptions<AuthOptions>>().Value.Mode == AuthMode.Development
                    ? SchemeNames.Development
                    : SchemeNames.Gateway)
            .AddScheme<AuthenticationSchemeOptions, GatewayAuthenticationHandler>(SchemeNames.Gateway, null)
            .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(SchemeNames.Development, null);

        services.AddTransient<IClaimsTransformation, CognitoGroupsClaimsTransformation>();
        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.CanRead, p => p.RequireRole(Roles.Viewer, Roles.Manager, Roles.Admin))
            .AddPolicy(Policies.CanWrite, p => p.RequireRole(Roles.Manager, Roles.Admin))
            .AddPolicy(Policies.CanAdminister, p => p.RequireRole(Roles.Admin));
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        return services;
    }
}
