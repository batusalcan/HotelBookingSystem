using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace NotificationService.Health;

/// <summary>
/// Verifies RabbitMQ connectivity by opening a short-lived connection.
/// Precondition: RabbitMQ config keys are set in appsettings.
/// Postcondition: Returns Healthy if connection succeeds, Unhealthy otherwise.
/// </summary>
public class RabbitMqHealthCheck(IConfiguration config) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = config["RabbitMQ:Host"] ?? "localhost",
                Port = int.Parse(config["RabbitMQ:Port"] ?? "5672"),
                UserName = config["RabbitMQ:Username"] ?? "guest",
                Password = config["RabbitMQ:Password"] ?? "guest",
                VirtualHost = config["RabbitMQ:VirtualHost"] ?? "/",
                Ssl = new SslOption
                {
                    Enabled = bool.Parse(config["RabbitMQ:Ssl"] ?? "false"),
                    ServerName = config["RabbitMQ:Host"] ?? "localhost"
                },
                RequestedConnectionTimeout = TimeSpan.FromSeconds(3)
            };
            using var conn = factory.CreateConnection();
            return Task.FromResult(HealthCheckResult.Healthy("RabbitMQ connection successful"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ unreachable", ex));
        }
    }
}
