using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.DynamoDb;

namespace PDS.IntegrationTests;

/// <summary>One API host plus one DynamoDB Local container, shared by every test in the "api" collection.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly DynamoDbContainer _dynamo = new DynamoDbBuilder("amazon/dynamodb-local:3.3.1").Build();

    public async Task InitializeAsync() => await _dynamo.StartAsync();

    Task IAsyncLifetime.DisposeAsync() => _dynamo.DisposeAsync().AsTask();

    /// <summary>Client signed in with the given comma-separated roles ("" = signed in with no groups).</summary>
    public HttpClient CreateClientAs(string roles)
    {
        var client = CreateClient();
        // HttpClient/TestServer silently drop a header whose value is the empty string, so an empty role list is
        // sent as a single space; TestAuthHandler's bracketed-group parsing still yields zero groups for it.
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, roles.Length == 0 ? " " : roles);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Dynamo:ServiceUrl", _dynamo.GetConnectionString());
        builder.UseSetting("Dynamo:CreateTablesOnStartup", "true");
        builder.UseSetting("Tables:portfolio", "test-portfolio");
        builder.ConfigureTestServices(services => services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { }));
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
