using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.Runtime;
using Microsoft.Extensions.Options;
using PDS.Shared.People;

namespace PDS.Api.People;

public static class PeopleServiceCollectionExtensions
{
    public static IServiceCollection AddPeopleDirectory(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PeopleOptions>().Bind(configuration.GetSection(PeopleOptions.Section));
        // Created lazily: only the Cognito directory resolves it, so local dev and OpenAPI generation never do.
        services.AddSingleton<IAmazonCognitoIdentityProvider>(_ =>
        {
            try
            {
                return new AmazonCognitoIdentityProviderClient();
            }
            catch (AmazonClientException)
            {
                return new AmazonCognitoIdentityProviderClient(RegionEndpoint.APSoutheast2);
            }
        });
        services.AddSingleton<ConfiguredPeopleDirectory>();
        services.AddSingleton<CognitoPeopleDirectory>();
        services.AddSingleton<IPeopleDirectory>(sp => new CachedPeopleDirectory(
            string.IsNullOrWhiteSpace(sp.GetRequiredService<IOptions<PeopleOptions>>().Value.UserPoolId)
                ? sp.GetRequiredService<ConfiguredPeopleDirectory>()
                : sp.GetRequiredService<CognitoPeopleDirectory>(),
            sp.GetRequiredService<TimeProvider>()));
        return services;
    }
}
