using Moq;
using RediKit.Abstractions;
using RediKit.Configuration;
using RediKit.Redis;
using StackExchange.Redis;

namespace RediKit.UnitTests.Redis;

public class RedisLockServiceTests
{
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly RedisLockService _lockService;
    private readonly RedisOptions _redisOptions;

    public RedisLockServiceTests()
    {
        var mockDatabaseAccessor = new Mock<IRedisDatabaseAccessor>();
        _mockDatabase = new Mock<IDatabase>();
        _redisOptions = new RedisOptions
        {
            ConnectionString = "localhost:6379",
            KeyPrefix = "test"
        };

        mockDatabaseAccessor.Setup(x => x.GetDatabase()).Returns(_mockDatabase.Object);
        mockDatabaseAccessor.Setup(x => x.Options).Returns(_redisOptions);

        _lockService = new RedisLockService(mockDatabaseAccessor.Object);
    }

    [Fact]
    public async Task AcquireAsync_WithValidKeyAndTtl_ReturnsLockToken()
    {
        // Arrange
        var key = "test-lock";
        var ttl = TimeSpan.FromMinutes(5);
        var expectedLockKey = new RedisKey("test:lock:test-lock");

        _mockDatabase.Setup(x => x.StringSetAsync(
            expectedLockKey,
            It.IsAny<RedisValue>(),
            ttl,
            When.NotExists))
            .ReturnsAsync(true);

        // Act
        var result = await _lockService.AcquireAsync(key, ttl);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Equal(32, result.Length); // 16 bytes converted to hex = 32 characters
        _mockDatabase.Verify(x => x.StringSetAsync(
            expectedLockKey,
            It.IsAny<RedisValue>(),
            ttl,
            When.NotExists), Times.Once);
    }

    [Fact]
    public async Task AcquireAsync_WhenLockAlreadyExists_ReturnsNull()
    {
        // Arrange
        var key = "existing-lock";
        var ttl = TimeSpan.FromMinutes(5);
        var expectedLockKey = new RedisKey("test:lock:existing-lock");

        _mockDatabase.Setup(x => x.StringSetAsync(
            expectedLockKey,
            It.IsAny<RedisValue>(),
            ttl,
            When.NotExists))
            .ReturnsAsync(false);

        // Act
        var result = await _lockService.AcquireAsync(key, ttl);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AcquireAsync_WithNullOrWhitespaceKey_ThrowsArgumentException()
    {
        // Arrange
        var ttl = TimeSpan.FromMinutes(5);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _lockService.AcquireAsync(null!, ttl));
        await Assert.ThrowsAsync<ArgumentException>(() => _lockService.AcquireAsync("", ttl));
        await Assert.ThrowsAsync<ArgumentException>(() => _lockService.AcquireAsync("   ", ttl));
    }

    [Fact]
    public async Task AcquireAsync_WithZeroOrNegativeTtl_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var key = "test-lock";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _lockService.AcquireAsync(key, TimeSpan.Zero));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _lockService.AcquireAsync(key, TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public async Task AcquireAsync_WithNoKeyPrefix_UsesCorrectLockKey()
    {
        // Arrange
        var key = "test-lock";
        var ttl = TimeSpan.FromMinutes(5);
        var expectedLockKey = new RedisKey("lock:test-lock");

        _redisOptions.KeyPrefix = null;

        _mockDatabase.Setup(x => x.StringSetAsync(
            expectedLockKey,
            It.IsAny<RedisValue>(),
            ttl,
            When.NotExists))
            .ReturnsAsync(true);

        // Act
        var result = await _lockService.AcquireAsync(key, ttl);

        // Assert
        Assert.NotNull(result);
        _mockDatabase.Verify(x => x.StringSetAsync(
            expectedLockKey,
            It.IsAny<RedisValue>(),
            ttl,
            When.NotExists), Times.Once);
    }

    [Fact]
    public async Task AcquireAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var key = "test-lock";
        var ttl = TimeSpan.FromMinutes(5);
        using var cts = new CancellationTokenSource();

        _mockDatabase.Setup(x => x.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            ttl,
            When.NotExists))
            .ReturnsAsync(true);

        // Act
        var result = await _lockService.AcquireAsync(key, ttl, cts.Token);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ReleaseAsync_WithValidKeyAndToken_ReturnsTrue()
    {
        // Arrange
        var key = "test-lock";
        var token = "valid-token";
        var expectedLockKey = new RedisKey("test:lock:test-lock");

        _mockDatabase.Setup(x => x.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.Is<RedisKey[]>(keys => keys[0] == expectedLockKey),
            It.Is<RedisValue[]>(values => values[0] == token),
            CommandFlags.None))
            .ReturnsAsync(RedisResult.Create(1L));

        // Act
        var result = await _lockService.ReleaseAsync(key, token);

        // Assert
        Assert.True(result);
        _mockDatabase.Verify(x => x.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.Is<RedisKey[]>(keys => keys[0] == expectedLockKey),
            It.Is<RedisValue[]>(values => values[0] == token),
            CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task ReleaseAsync_WithInvalidToken_ReturnsFalse()
    {
        // Arrange
        var key = "test-lock";
        var token = "invalid-token";
        var expectedLockKey = new RedisKey("test:lock:test-lock");

        _mockDatabase.Setup(x => x.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.Is<RedisKey[]>(keys => keys[0] == expectedLockKey),
            It.Is<RedisValue[]>(values => values[0] == token),
            CommandFlags.None))
            .ReturnsAsync(RedisResult.Create(0L));

        // Act
        var result = await _lockService.ReleaseAsync(key, token);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ReleaseAsync_WithNullOrWhitespaceKey_ThrowsArgumentException()
    {
        // Arrange
        var token = "valid-token";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException> (() => _lockService.ReleaseAsync(null!, token));
        await Assert.ThrowsAsync<ArgumentException>(() => _lockService.ReleaseAsync("", token));
        await Assert.ThrowsAsync<ArgumentException>(() => _lockService.ReleaseAsync("   ", token));
    }

    [Fact]
    public async Task ReleaseAsync_WithNullOrWhitespaceToken_ThrowsArgumentException()
    {
        // Arrange
        var key = "test-lock";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _lockService.ReleaseAsync(key, null!));
        await Assert.ThrowsAsync<ArgumentException>(() => _lockService.ReleaseAsync(key, ""));
        await Assert.ThrowsAsync<ArgumentException>(() => _lockService.ReleaseAsync(key, "   "));
    }

    [Fact]
    public async Task ReleaseAsync_WithNoKeyPrefix_UsesCorrectLockKey()
    {
        // Arrange
        var key = "test-lock";
        var token = "valid-token";
        var expectedLockKey = new RedisKey("lock:test-lock");

        _redisOptions.KeyPrefix = null;

        _mockDatabase.Setup(x => x.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.Is<RedisKey[]>(keys => keys[0] == expectedLockKey),
            It.Is<RedisValue[]>(values => values[0] == token),
            CommandFlags.None))
            .ReturnsAsync(RedisResult.Create(1L));

        // Act
        var result = await _lockService.ReleaseAsync(key, token);

        // Assert
        Assert.True(result);
        _mockDatabase.Verify(x => x.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.Is<RedisKey[]>(keys => keys[0] == expectedLockKey),
            It.Is<RedisValue[]>(values => values[0] == token),
            CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task ReleaseAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var key = "test-lock";
        var token = "valid-token";
        using var cts = new CancellationTokenSource();

        _mockDatabase.Setup(x => x.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            CommandFlags.None))
            .ReturnsAsync(RedisResult.Create(1L));

        // Act
        var result = await _lockService.ReleaseAsync(key, token, cts.Token);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task AcquireAsync_ReleaseAsync_FullWorkflow_WorksCorrectly()
    {
        // Arrange
        var key = "workflow-test";
        var ttl = TimeSpan.FromMinutes(5);
        var expectedLockKey = new RedisKey("test:lock:workflow-test");

        // Setup acquire to succeed
        _mockDatabase.Setup(x => x.StringSetAsync(
            expectedLockKey,
            It.IsAny<RedisValue>(),
            ttl,
            When.NotExists))
            .ReturnsAsync(true);

        // Setup release to succeed
        _mockDatabase.Setup(x => x.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.Is<RedisKey[]>(keys => keys[0] == expectedLockKey),
            It.IsAny<RedisValue[]>(),
            CommandFlags.None))
            .ReturnsAsync(RedisResult.Create(1L));

        // Act
        var token = await _lockService.AcquireAsync(key, ttl);
        var released = await _lockService.ReleaseAsync(key, token!);

        // Assert
        Assert.NotNull(token);
        Assert.True(released);
    }

    [Fact]
    public async Task AcquireAsync_GeneratesUniqueTokens_ReturnsUniqueValues()
    {
        // Arrange
        var key = "unique-test";
        var ttl = TimeSpan.FromMinutes(5);

        _mockDatabase.Setup(x => x.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            ttl,
            When.NotExists
            ))
            .ReturnsAsync(true);

        // Act
        var token1 = await _lockService.AcquireAsync($"{key}-1", ttl);
        var token2 = await _lockService.AcquireAsync($"{key}-2", ttl);

        // Assert
        Assert.NotNull(token1);
        Assert.NotNull(token2);
        Assert.NotEqual(token1, token2);
    }

    [Theory]
    [InlineData("simple")]
    [InlineData("with-dashes")]
    [InlineData("with:colons")]
    [InlineData("with.dots")]
    [InlineData("with_underscores")]
    public async Task AcquireAsync_WithDifferentKeyFormats_HandlesCorrectly(string key)
    {
        // Arrange
        var ttl = TimeSpan.FromMinutes(5);

        _mockDatabase.Setup(x => x.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            ttl,
            When.NotExists))
            .ReturnsAsync(true);

        // Act
        var result = await _lockService.AcquireAsync(key, ttl);

        // Assert
        Assert.NotNull(result);
        _mockDatabase.Verify(x => x.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            ttl,
            When.NotExists), Times.Once);
    }
}