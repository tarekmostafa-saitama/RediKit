using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RediKit.Configuration;
using RediKit.Redis;

namespace RediKit.HealthChecks;

/// <summary>
/// Health check for Redis connectivity and basic operations.
/// </summary>
internal sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IRedisDatabaseAccessor _database;
    private readonly IOptions<CacheOptions> _options;

    public RedisHealthCheck(IRedisDatabaseAccessor database, IOptions<CacheOptions> options)
    {
        _database = database;
        _options = options;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var data = new Dictionary<string, object>
            {
                ["connection_string"] = MaskConnectionString(_options.Value.Redis.ConnectionString),
                ["key_prefix"] = _options.Value.Redis.KeyPrefix ?? "none"
            };

            // Test basic connectivity with a simple ping-like operation
            var testKey = $"{_options.Value.Redis.KeyPrefix}:healthcheck:{Guid.NewGuid():N}";
            var testValue = DateTimeOffset.UtcNow.ToString("O");

            // Test write
            var setResult = await _database.StringSetAsync(testKey, testValue, TimeSpan.FromSeconds(5));
            if (!setResult)
            {
                return HealthCheckResult.Unhealthy("Failed to write test value to Redis", null, data);
            }

            // Test read
            var getValue = await _database.StringGetAsync(testKey);
            if (!getValue.HasValue || getValue != testValue)
            {
                return HealthCheckResult.Unhealthy("Failed to read test value from Redis", null, data);
            }

            // Test delete
            var deleteResult = await _database.KeyDeleteAsync(testKey);
            if (!deleteResult)
            {
                return HealthCheckResult.Degraded("Could not clean up test key, but Redis is accessible", null, data);
            }

            data["test_key"] = testKey;
            data["round_trip_successful"] = true;

            return HealthCheckResult.Healthy("Redis is accessible and responding correctly", data);
        }
        catch (TimeoutException ex)
        {
            return HealthCheckResult.Degraded($"Redis connection timeout: {ex.Message}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Redis health check failed: {ex.Message}", ex);
        }
    }

    private static string MaskConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return "not_configured";

        // Simple masking - hide passwords if present
        var parts = connectionString.Split(',');
        var maskedParts = new List<string>();

        foreach (var part in parts)
        {
            if (part.Trim().StartsWith("password=", StringComparison.OrdinalIgnoreCase))
            {
                maskedParts.Add("password=***");
            }
            else
            {
                maskedParts.Add(part);
            }
        }

        return string.Join(',', maskedParts);
    }
}
