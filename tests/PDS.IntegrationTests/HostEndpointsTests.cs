using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PDS.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class HostEndpointsTests(ApiFactory factory)
{
    [Fact]
    public async Task Config_is_anonymous_and_returns_deployment_defaults()
    {
        var config = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/config");

        Assert.Equal("Property Development System", config.GetProperty("productName").GetString());
        Assert.Equal("teal", config.GetProperty("primaryColor").GetString());
        Assert.Equal("NZD", config.GetProperty("currency").GetString());
        Assert.Equal("en-NZ", config.GetProperty("culture").GetString());
    }

    [Fact]
    public async Task Valid_correlation_id_is_echoed()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/config");
        request.Headers.Add("X-Correlation-Id", "abc-123");

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal("abc-123", response.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Theory]
    [InlineData("bad id!")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task Invalid_correlation_id_is_replaced(string incoming)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/config");
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", incoming);

        var response = await factory.CreateClient().SendAsync(request);

        var echoed = response.Headers.GetValues("X-Correlation-Id").Single();
        Assert.NotEqual(incoming, echoed);
        Assert.Equal(32, echoed.Length);
    }

    [Fact]
    public async Task Unknown_route_returns_problem_details()
    {
        var response = await factory.CreateClient().GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.TryGetProperty("correlationId", out _));
    }
}
