using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace NotificationService.Tests;

/// <summary>
/// Smoke test: verifies /health endpoint responds with a JSON body containing "status".
/// The service responds Healthy when RabbitMQ is reachable, Unhealthy when not.
/// This test only checks that the endpoint is registered and returns a parseable response.
/// </summary>
public class HealthCheckTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_Endpoint_Returns_StatusField()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("status", body, StringComparison.OrdinalIgnoreCase);
    }
}
