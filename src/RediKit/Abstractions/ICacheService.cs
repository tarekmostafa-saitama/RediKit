
namespace Common.Cache.Abstractions;

/// <summary>
/// Abstraction for a high-performance distributed cache.
/// Consumers should not depend on any specific backing store implementation.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Gets a value from the cache by <paramref name="key"/> and deserializes it to <typeparamref name="T"/>.
    /// Returns default when the key does not exist.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a value in the cache with an optional absolute expiration.
    /// When <paramref name="absoluteExpiration"/> is null, a default expiration policy is applied if configured.
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiration = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a value from cache.
    /// </summary>
    Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);
}


