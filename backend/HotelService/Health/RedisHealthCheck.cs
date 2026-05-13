using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace HotelService.Health;

public class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = redis.GetDatabase();
            await db.PingAsync();
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded(exception: ex);
        }
    }
}
