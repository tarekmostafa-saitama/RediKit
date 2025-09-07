using System.Security.Cryptography;
using RediKit.Abstractions;
using StackExchange.Redis;

namespace RediKit.Redis;

internal sealed class RedisLockService : IRedisLockService
{
    private static readonly string ReleaseScript = @"
if redis.call('get', KEYS[1]) == ARGV[1] then
  return redis.call('del', KEYS[1])
else
  return 0
end";

    private readonly IRedisDatabaseAccessor _connectionProvider;

    public RedisLockService(IRedisDatabaseAccessor connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }

    public async Task<string?> AcquireAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (ttl <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ttl));

        var db = _connectionProvider.GetDatabase();
        var lockKey = BuildLockKey(key);
        string token = GenerateToken();

        bool acquired = await db.StringSetAsync(lockKey, token, ttl, When.NotExists).ConfigureAwait(false);
        return acquired ? token : null;
    }

    public async Task<bool> ReleaseAsync(string key, string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var db = _connectionProvider.GetDatabase();
        var lockKey = BuildLockKey(key);

        var result = (int) (long) (await db.ScriptEvaluateAsync(ReleaseScript, new RedisKey[] { lockKey }, new RedisValue[] { token }).ConfigureAwait(false));
        return result == 1;
    }

    private RedisKey BuildLockKey(string key)
    {
        var prefix = _connectionProvider.Options.KeyPrefix;
        var fullKey = string.IsNullOrEmpty(prefix) ? key : prefix + ":lock:" + key;
        if (string.IsNullOrEmpty(prefix))
        {
            return new RedisKey("lock:" + key);
        }
        return new RedisKey(fullKey);
    }

    private static string GenerateToken()
    {
        Span<byte> buffer = stackalloc byte[16];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToHexString(buffer).ToLowerInvariant();
    }
}


