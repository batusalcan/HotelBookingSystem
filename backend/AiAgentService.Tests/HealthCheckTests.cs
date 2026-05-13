using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AiAgentService.Tests;

/// <summary>
/// Smoke test: verifies /health endpoint responds with a JSON body containing "status".
/// AiAgentService has no external DB so health check is always Healthy.
/// </summary>
public class HealthCheckTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_Endpoint_Returns_Healthy()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Healthy", body, StringComparison.OrdinalIgnoreCase);
    }
}
