using RediKit.Configuration;
using StackExchange.Redis;

namespace RediKit.Redis;

internal interface IRedisDatabaseAccessor
{
    RedisOptions Options { get; }
    IDatabase GetDatabase();
    
    // Redis operations for health checks and caching
    Task<RedisValue> StringGetAsync(RedisKey key, CommandFlags flags = CommandFlags.None);
    Task<bool> StringSetAsync(RedisKey key, RedisValue value, TimeSpan? expiry = null, When when = When.Always, CommandFlags flags = CommandFlags.None);
    Task<bool> KeyDeleteAsync(RedisKey key, CommandFlags flags = CommandFlags.None);
    Task<RedisResult> ScriptEvaluateAsync(string script, RedisKey[]? keys = null, RedisValue[]? values = null, CommandFlags flags = CommandFlags.None);
}


