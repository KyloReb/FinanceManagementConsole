namespace FMC.Application.Interfaces;

/// <summary>
/// Simple interface for distributed caching operations.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Retrieves a value from the cache.
    /// </summary>
    Task<T?> GetAsync<T>(string key);

    /// <summary>
    /// Sets a value in the cache with a specified expiration.
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);

    /// <summary>
    /// Removes a value from the cache.
    /// </summary>
    Task RemoveAsync(string key);
}
