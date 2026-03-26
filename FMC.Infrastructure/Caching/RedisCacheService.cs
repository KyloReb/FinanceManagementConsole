using System.Text.Json;
using FMC.Application.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace FMC.Infrastructure.Caching;

/// <summary>
/// Implementation of ICacheService using ASP.NET Core IDistributedCache (Redis).
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;

    public RedisCacheService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var cachedValue = await _cache.GetStringAsync(key);
        if (cachedValue == null) return default;

        return JsonSerializer.Deserialize<T>(cachedValue);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromHours(1)
        };

        var serializedValue = JsonSerializer.Serialize(value);
        await _cache.SetStringAsync(key, serializedValue, options);
    }

    public async Task RemoveAsync(string key)
    {
        await _cache.RemoveAsync(key);
    }
}
