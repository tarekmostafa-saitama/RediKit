namespace RediKit.Configuration;

/// <summary>
/// Health check configuration options.
/// </summary>
public sealed class HealthCheckOptions
{
    /// <summary>
    /// Whether to enable Redis health checks.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Health check timeout in milliseconds.
    /// </summary>
    public int TimeoutMs { get; set; } = 3_000;

    /// <summary>
    /// Tags to apply to the health check.
    /// </summary>
    public string[] Tags { get; set; } = { "cache", "redis" };
}
