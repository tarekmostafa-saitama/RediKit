using Common.Cache.Abstractions;
using Common.Cache.Serialization;
using StackExchange.Redis;

namespace Common.Cache.Redis;


internal sealed class RedisCacheService(IRedisDatabaseAccessor connectionProvider, ICacheSerializer serializer)
    : ICacheService
{
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var db = connectionProvider.GetDatabase();
        var redisKey = BuildKey(key);

        var value = await db.StringGetAsync(redisKey).ConfigureAwait(false);
        if (value.IsNullOrEmpty)
        {
            return default;
        }

        var bytes = (byte[]?)value;
        if (bytes is null || bytes.Length == 0)
        {
            return default;
        }

        return await serializer.DeserializeAsync<T>(bytes, cancellationToken);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiration = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var db = connectionProvider.GetDatabase();
        var redisKey = BuildKey(key);

        var payload = await serializer.SerializeAsync(value, cancellationToken);
        var expiry = absoluteExpiration ?? connectionProvider.Options.DefaultExpiration;
        await db.StringSetAsync(redisKey, payload, expiry).ConfigureAwait(false);
    }

    public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var db = connectionProvider.GetDatabase();
        var redisKey = BuildKey(key);
        return await db.KeyDeleteAsync(redisKey).ConfigureAwait(false);
    }

    private RedisKey BuildKey(string key)
    {
        var prefix = connectionProvider.Options.KeyPrefix;
        return string.IsNullOrEmpty(prefix) ? new RedisKey(key) : new RedisKey(prefix + ":" + key);
    }
}


