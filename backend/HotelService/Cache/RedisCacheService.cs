using StackExchange.Redis;

namespace HotelService.Cache;

/// <summary>
/// Singleton wrapper around StackExchange.Redis ConnectionMultiplexer.
/// One connection pool per service instance per the Singleton pattern requirement.
/// Fails gracefully — cache misses on Redis unavailability; SQL fallback takes over.
/// </summary>
public class RedisCacheService(IConnectionMultiplexer multiplexer, ILogger<RedisCacheService> logger) : ICacheService
{
    private readonly IDatabase _db = multiplexer.GetDatabase();
    private IServer Server => multiplexer.GetServer(multiplexer.GetEndPoints().First());

    public async Task<string?> GetAsync(string key)
    {
        try
        {
            var value = await _db.StringGetAsync(key);
            return value.IsNullOrEmpty ? null : value.ToString();
        }
        catch (Exception ex)
        {
            logger.LogWarning("Redis GET failed for key {Key}: {Message}", key, ex.Message);
            return null;
        }
    }

    public async Task SetAsync(string key, string value, TimeSpan ttl)
    {
        try
        {
            await _db.StringSetAsync(key, value, ttl);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Redis SET failed for key {Key}: {Message}", key, ex.Message);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Redis DEL failed for key {Key}: {Message}", key, ex.Message);
        }
    }

    public async Task RemoveByPatternAsync(string pattern)
    {
        try
        {
            var keys = Server.KeysAsync(pattern: pattern);
            await foreach (var key in keys)
                await _db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Redis DEL pattern {Pattern} failed: {Message}", pattern, ex.Message);
        }
    }
}
