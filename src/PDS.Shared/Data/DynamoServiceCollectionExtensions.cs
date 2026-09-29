using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PDS.Shared.Data;

public static class DynamoServiceCollectionExtensions
{
    /// <summary>
    /// Registers the DynamoDB client. With Dynamo:ServiceUrl set (DynamoDB Local), it uses dummy credentials;
    /// otherwise it uses the default AWS credential chain and region (the Lambda role in AWS). When no region can
    /// be resolved at all (e.g. build-time OpenAPI document generation, which boots the host with no AWS_REGION
    /// set), falls back to ap-southeast-2 so client construction never throws; this client is never called unless
    /// Dynamo:CreateTablesOnStartup is true or a request is served.
    /// </summary>
    public static IServiceCollection AddDynamo(this IServiceCollection services)
    {
        services.AddSingleton<IAmazonDynamoDB>(sp =>
        {
            var serviceUrl = sp.GetRequiredService<IConfiguration>()["Dynamo:ServiceUrl"];
            if (string.IsNullOrEmpty(serviceUrl))
            {
                try
                {
                    return new AmazonDynamoDBClient();
                }
                catch (AmazonClientException)
                {
                    return new AmazonDynamoDBClient(new AmazonDynamoDBConfig { RegionEndpoint = RegionEndpoint.APSoutheast2 });
                }
            }

            return new AmazonDynamoDBClient(
                new BasicAWSCredentials("local", "local"),
                new AmazonDynamoDBConfig { ServiceURL = serviceUrl, AuthenticationRegion = "ap-southeast-2" });
        });
        services.AddSingleton<TableNames>();
        services.AddSingleton<TableBootstrapper>();
        services.AddHostedService<TableBootstrapperHostedService>();
        return services;
    }
}
