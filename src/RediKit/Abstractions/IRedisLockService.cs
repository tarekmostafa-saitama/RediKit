namespace Common.Cache.Abstractions;

/// <summary>
/// Abstraction for a distributed lock backed by Redis.
/// </summary>
public interface IRedisLockService
{
    /// <summary>
    /// Attempts to acquire a distributed lock for the given key.
    /// Returns a lock token that must be used to release the lock.
    /// </summary>
    Task<string?> AcquireAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a previously acquired lock if the provided token matches.
    /// </summary>
    Task<bool> ReleaseAsync(string key, string token, CancellationToken cancellationToken = default);
}


