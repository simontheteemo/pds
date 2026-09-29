using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PDS.Shared.Data;

public static class DynamoServiceCollectionExtensions
{
    /// <summary>
    /// Registers the DynamoDB client. With Dynamo:ServiceUrl set (DynamoDB Local), it uses dummy credentials;
    /// otherwise it uses the default AWS credential chain and region (the Lambda role in AWS).
    /// </summary>
    public static IServiceCollection AddDynamo(this IServiceCollection services)
    {
        services.AddSingleton<IAmazonDynamoDB>(sp =>
        {
            var serviceUrl = sp.GetRequiredService<IConfiguration>()["Dynamo:ServiceUrl"];
            if (string.IsNullOrEmpty(serviceUrl))
                return new AmazonDynamoDBClient();

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
