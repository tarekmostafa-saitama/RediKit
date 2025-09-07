namespace RediKit.Configuration;

/// <summary>
/// Root configuration options for the caching system.
/// </summary>
public sealed class CacheOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Cache";

    /// <summary>
    /// Redis-specific configuration.
    /// </summary>
    public RedisOptions Redis { get; set; } = new();

    /// <summary>
    /// Health check configuration.
    /// </summary>
    public HealthCheckOptions HealthChecks { get; set; } = new();
}
