using System.ComponentModel.DataAnnotations;

namespace RediKit.Configuration;

/// <summary>
/// Redis configuration options.
/// </summary>
public class RedisOptions
{
    /// <summary>
    /// Redis connection string, e.g., "localhost:6379".
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Optional prefix for all cache keys.
    /// </summary>
    public string? KeyPrefix { get; set; }

    /// <summary>
    /// Default cache expiration time.
    /// </summary>
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(5);
}
