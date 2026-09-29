using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PDS.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class SecurityTests(ApiFactory factory)
{
    [Fact]
    public async Task Me_requires_authentication()
    {
        var response = await factory.CreateClient().GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Me_returns_identity_and_roles()
    {
        var me = await factory.CreateClientAs("Manager").GetFromJsonAsync<JsonElement>("/api/me");

        Assert.Equal("test-user", me.GetProperty("id").GetString());
        Assert.Equal("Test User", me.GetProperty("name").GetString());
        Assert.Equal(new string?[] { "Manager" }, me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
    }

    [Fact]
    public async Task Signed_in_user_without_groups_is_forbidden()
    {
        var response = await factory.CreateClientAs("").GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Config_exposes_gateway_auth_mode()
    {
        var config = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/config");

        Assert.Equal("Gateway", config.GetProperty("auth").GetProperty("mode").GetString());
    }

    [Fact]
    public void Development_auth_mode_is_refused_outside_development()
    {
        using var production = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.UseSetting("Auth:Mode", "Development");
        });

        var error = Assert.ThrowsAny<Exception>(() => production.CreateClient());
        Assert.Contains("Development", error.ToString());
    }
}
