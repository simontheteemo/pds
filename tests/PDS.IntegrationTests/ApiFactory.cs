using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.DynamoDb;

namespace PDS.IntegrationTests;

/// <summary>One API host plus one DynamoDB Local container, shared by every test in the "api" collection.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly DynamoDbContainer _dynamo = new DynamoDbBuilder("amazon/dynamodb-local:3.3.1").Build();

    public async Task InitializeAsync() => await _dynamo.StartAsync();

    Task IAsyncLifetime.DisposeAsync() => _dynamo.DisposeAsync().AsTask();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Dynamo:ServiceUrl", _dynamo.GetConnectionString());
        builder.UseSetting("Dynamo:CreateTablesOnStartup", "true");
        builder.UseSetting("Tables:portfolio", "test-portfolio");
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
