using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RediKit.Abstractions;
using RediKit.Configuration;
using RediKit.HealthChecks;
using RediKit.Redis;
using RediKit.Serialization;

namespace RediKit.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Redis-based caching and distributed locking services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="configureOptions">Optional configuration delegate.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRedisCache(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        Action<CacheOptions>? configureOptions = null)
    {
        // Configure options from appsettings.json
        if (configuration != null)
        {
            services.Configure<CacheOptions>(options => 
                configuration.GetSection(CacheOptions.SectionName).Bind(options));
        }
        
        // Apply additional configuration
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }

        // Serialization
        services.AddSingleton<ICacheSerializer, CacheSerializer>();

        // Connection Provider (singleton)
        services.AddSingleton<RedisConnectionProvider>();
        services.AddSingleton<IRedisDatabaseAccessor>(sp => sp.GetRequiredService<RedisConnectionProvider>());

        // Cache + Lock services
        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddSingleton<IRedisLockService, RedisLockService>();

        // Health checks
        services.AddCacheHealthChecks();

        return services;
    }

    /// <summary>
    /// Registers Redis-based caching with fluent configuration only.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Configuration delegate.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRedisCache(this IServiceCollection services, Action<CacheOptions> configureOptions)
    {
        return services.AddRedisCache(configuration: null, configureOptions);
    }

    /// <summary>
    /// Adds health checks for caching infrastructure.
    /// </summary>
    private static IServiceCollection AddCacheHealthChecks(this IServiceCollection services)
    {
        services.AddSingleton<RedisHealthCheck>();
        
        services.AddHealthChecks()
            .AddCheck<RedisHealthCheck>("redis_cache", HealthStatus.Degraded, new[] { "cache", "redis" });

        return services;
    }
}