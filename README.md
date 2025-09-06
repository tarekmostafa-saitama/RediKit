# RediKit

> **Simple, high-performance Redis caching and distributed locking for .NET 9**

[![NuGet Version](https://img.shields.io/nuget/v/RediKit)](https://www.nuget.org/packages/RediKit)
[![Downloads](https://img.shields.io/nuget/dt/RediKit)](https://www.nuget.org/packages/RediKit)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

RediKit provides a clean, minimal API for Redis-based caching and distributed locking in .NET applications. Built with performance and simplicity in mind.

## ✨ Features

- **🚀 Zero-configuration setup** - Works with just a connection string
- **🔒 Distributed locking** - Atomic operations with automatic token generation
- **💾 JSON serialization** - Built-in System.Text.Json support
- **💚 Health monitoring** - Comprehensive health checks for Redis connectivity
- **🧪 Testable design** - Clean abstractions for easy unit testing
- **⚡ High performance** - Optimized connection pooling and async operations
- **🛡️ Production ready** - Proper timeout handling, retries, and error management

## 🚀 Quick Start

### Installation

```bash
dotnet add package RediKit
```

### Basic Setup

```csharp
using RediKit.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Minimal setup - just provide connection string
builder.Services.AddRedisCache(options =>
{
    options.Redis.ConnectionString = "localhost:6379";
});

var app = builder.Build();
```

### Configuration from appsettings.json

```json
{
  "Cache": {
    "Redis": {
      "ConnectionString": "localhost:6379",
      "KeyPrefix": "myapp",
      "DefaultExpiration": "00:05:00"
    },
    "HealthChecks": {
      "Enabled": true,
      "TimeoutMs": 3000,
      "Tags": ["cache", "redis"]
    }
  }
}
```

```csharp
// Load configuration from appsettings.json
builder.Services.AddRedisCache(builder.Configuration);
```

## 📖 Usage Guide

### Caching Operations

```csharp
using RediKit.Abstractions;

public class ProductService
{
    private readonly ICacheService _cache;
    
    public ProductService(ICacheService cache)
    {
        _cache = cache;
    }

    public async Task<Product?> GetProductAsync(int id)
    {
        var key = $"product:{id}";
        
        // Try to get from cache first
        var product = await _cache.GetAsync<Product>(key);
        if (product != null)
            return product;
            
        // Load from database
        product = await LoadProductFromDatabase(id);
        
        // Cache for 10 minutes
        await _cache.SetAsync(key, product, TimeSpan.FromMinutes(10));
        
        return product;
    }
    
    public async Task InvalidateProductAsync(int id)
    {
        var key = $"product:{id}";
        await _cache.RemoveAsync(key);
    }
}
```

### Distributed Locking

```csharp
using RediKit.Abstractions;

public class InventoryService
{
    private readonly IRedisLockService _lockService;
    
    public InventoryService(IRedisLockService lockService)
    {
        _lockService = lockService;
    }

    public async Task<bool> ReserveInventoryAsync(int productId, int quantity)
    {
        var lockKey = $"inventory:{productId}";
        var lockToken = await _lockService.AcquireAsync(lockKey, TimeSpan.FromSeconds(30));
        
        if (lockToken == null)
        {
            // Could not acquire lock - another process is updating inventory
            return false;
        }
        
        try
        {
            // Critical section - update inventory
            var success = await UpdateInventoryInDatabase(productId, quantity);
            return success;
        }
        finally
        {
            // Always release the lock
            await _lockService.ReleaseAsync(lockKey, lockToken);
        }
    }
}
```

### Health Checks

```csharp
// Health checks are automatically registered
app.MapHealthChecks("/health");

// Or use with more detailed configuration
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

## ⚙️ Configuration Options

### Redis Options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ConnectionString` | `string` | `"localhost:6379"` | Redis connection string |
| `KeyPrefix` | `string?` | `null` | Optional prefix for all cache keys |
| `DefaultExpiration` | `TimeSpan` | `5 minutes` | Default TTL for cached items |

### Health Check Options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | `bool` | `true` | Enable/disable health checks |
| `TimeoutMs` | `int` | `3000` | Health check timeout in milliseconds |
| `Tags` | `string[]` | `["cache", "redis"]` | Tags for health check filtering |

### Advanced Configuration

```csharp
builder.Services.AddRedisCache(options =>
{
    options.Redis.ConnectionString = "localhost:6379";
    options.Redis.KeyPrefix = "myapp";
    options.Redis.DefaultExpiration = TimeSpan.FromMinutes(15);
    
    options.HealthChecks.Enabled = true;
    options.HealthChecks.TimeoutMs = 5000;
    options.HealthChecks.Tags = new[] { "cache", "redis", "critical" };
});
```

## 🏗️ Architecture

RediKit follows clean architecture principles with clear separation of concerns:

```
├── Abstractions/          # Public interfaces
│   ├── ICacheService
│   └── IRedisLockService
├── Configuration/         # Configuration models
│   ├── CacheOptions
│   ├── RedisOptions
│   └── HealthCheckOptions
├── Extensions/            # Dependency injection setup
│   └── ServiceCollectionExtensions
├── Redis/                 # Redis implementations
│   ├── RedisCacheService
│   ├── RedisLockService
│   ├── RedisConnectionProvider
│   └── IRedisDatabaseAccessor
├── Serialization/         # JSON serialization
│   └── CacheSerializer
└── HealthChecks/          # Health monitoring
    └── RedisHealthCheck
```

### Key Design Decisions

- **Interface segregation** - Separate interfaces for caching vs locking
- **Dependency injection friendly** - All services registered as singletons
- **Connection pooling** - Single connection multiplexer per application
- **Atomic operations** - Lua scripts for safe lock release
- **Async-first** - All operations are fully asynchronous
- **Memory efficient** - Optimized serialization with minimal allocations

## 🔒 Security & Best Practices

### Distributed Locking

- Uses cryptographically secure random tokens
- Lua script ensures atomic lock release
- Automatic timeout prevents deadlocks
- Token validation prevents accidental releases

### Connection Management

- Single connection multiplexer (thread-safe)
- Automatic reconnection on failures
- Configurable timeouts and retries
- Graceful degradation on connection issues

### Serialization

- Uses System.Text.Json for performance
- Handles null values gracefully
- Memory-efficient byte array operations
- Async serialization prevents blocking

## 🧪 Testing

RediKit provides clean abstractions that make testing easy:

```csharp
// Unit test example
[Test]
public async Task ProductService_ShouldCacheResults()
{
    // Arrange
    var mockCache = new Mock<ICacheService>();
    var service = new ProductService(mockCache.Object);
    
    // Act & Assert
    mockCache.Setup(x => x.GetAsync<Product>("product:1", default))
           .ReturnsAsync(new Product { Id = 1, Name = "Test" });
           
    var result = await service.GetProductAsync(1);
    
    Assert.That(result?.Name, Is.EqualTo("Test"));
}
```

## 📦 Package Information

- **Target Framework**: .NET 9.0
- **Dependencies**: 
  - StackExchange.Redis 2.7.33
  - Microsoft.Extensions.* 9.0.0
- **Package Size**: ~20KB
- **Symbols**: Available for debugging

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

**Made with ❤️ for the .NET community**