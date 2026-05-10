namespace HotelService.Cache;

public interface ICacheService
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value, TimeSpan ttl);
    Task RemoveAsync(string key);
}
