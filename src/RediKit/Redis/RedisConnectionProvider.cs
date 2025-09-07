using Microsoft.Extensions.Options;
using RediKit.Configuration;
using StackExchange.Redis;

namespace RediKit.Redis;

internal sealed class RedisConnectionProvider : IRedisDatabaseAccessor, IDisposable
{
    private readonly Lazy<ConnectionMultiplexer> _lazyMultiplexer;
    private bool _disposed;

    public RedisConnectionProvider(IOptions<CacheOptions> options)
    {
        if (options.Value is null) throw new ArgumentNullException(nameof(options));

        _lazyMultiplexer = new Lazy<ConnectionMultiplexer>(() =>
        {
            var redisOptions = options.Value.Redis;
            var cfg = ConfigurationOptions.Parse(redisOptions.ConnectionString, false);
            
            // Use sensible defaults for connection settings
            cfg.ConnectTimeout = 5000;      // 5 seconds
            cfg.AsyncTimeout = 5000;        // 5 seconds  
            cfg.SyncTimeout = 5000;         // 5 seconds
            cfg.ConnectRetry = 3;           // 3 retries
            cfg.KeepAlive = 60;             // 60 seconds
            cfg.AbortOnConnectFail = false; // Don't abort on first failure
            
            return ConnectionMultiplexer.Connect(cfg);
        }, isThreadSafe: true);

        Options = options.Value.Redis;
    }

    public RedisOptions Options { get; }

    private IConnectionMultiplexer GetConnection() => _lazyMultiplexer.Value;

    public IDatabase GetDatabase()
    {
        var connection = GetConnection();
        return connection.GetDatabase();
    }

    // IRedisDatabaseAccessor implementation
    public Task<RedisValue> StringGetAsync(RedisKey key, CommandFlags flags = CommandFlags.None)
    {
        return GetDatabase().StringGetAsync(key, flags);
    }

    public Task<bool> StringSetAsync(RedisKey key, RedisValue value, TimeSpan? expiry = null, When when = When.Always, CommandFlags flags = CommandFlags.None)
    {
        return GetDatabase().StringSetAsync(key, value, expiry, when, flags);
    }

    public Task<bool> KeyDeleteAsync(RedisKey key, CommandFlags flags = CommandFlags.None)
    {
        return GetDatabase().KeyDeleteAsync(key, flags);
    }

    public Task<RedisResult> ScriptEvaluateAsync(string script, RedisKey[]? keys = null, RedisValue[]? values = null, CommandFlags flags = CommandFlags.None)
    {
        return GetDatabase().ScriptEvaluateAsync(script, keys, values, flags);
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (_lazyMultiplexer.IsValueCreated)
        {
            _lazyMultiplexer.Value.Dispose();
        }
        _disposed = true;
    }
}


