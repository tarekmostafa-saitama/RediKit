using Moq;
using RediKit.Abstractions;
using RediKit.Configuration;
using RediKit.Redis;
using RediKit.Serialization;
using StackExchange.Redis;

namespace RediKit.UnitTests.Redis;

public class RedisCacheServiceTests
{
    private readonly Mock<IRedisDatabaseAccessor> _mockDatabaseAccessor;
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<ICacheSerializer> _mockSerializer;
    private readonly RedisCacheService _cacheService;
    private readonly RedisOptions _redisOptions;

    public RedisCacheServiceTests()
    {
        _mockDatabaseAccessor = new Mock<IRedisDatabaseAccessor>();
        _mockDatabase = new Mock<IDatabase>();
        _mockSerializer = new Mock<ICacheSerializer>();
        _redisOptions = new RedisOptions
        {
            ConnectionString = "localhost:6379",
            KeyPrefix = "test",
            DefaultExpiration = TimeSpan.FromMinutes(30)
        };

        _mockDatabaseAccessor.Setup(x => x.GetDatabase()).Returns(_mockDatabase.Object);
        _mockDatabaseAccessor.Setup(x => x.Options).Returns(_redisOptions);

        _cacheService = new RedisCacheService(_mockDatabaseAccessor.Object, _mockSerializer.Object);
    }

    #region GetAsync Tests

    [Fact]
    public async Task GetAsync_WithValidKey_ReturnsDeserializedValue()
    {
        // Arrange
        var key = "test-key";
        var expectedRedisKey = new RedisKey("test:test-key");
        var testValue = new TestObject { Id = 1, Name = "Test" };
        var serializedBytes = new byte[] { 1, 2, 3, 4 };

        _mockDatabase.Setup(x => x.StringGetAsync(expectedRedisKey, CommandFlags.None))
            .ReturnsAsync((RedisValue)serializedBytes);

        _mockSerializer.Setup(x => x.DeserializeAsync<TestObject>(
            It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testValue);

        // Act
        var result = await _cacheService.GetAsync<TestObject>(key);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(testValue.Id, result.Id);
        Assert.Equal(testValue.Name, result.Name);

        _mockDatabase.Verify(x => x.StringGetAsync(expectedRedisKey, CommandFlags.None), Times.Once);
        _mockSerializer.Verify(x => x.DeserializeAsync<TestObject>(
            It.Is<ReadOnlyMemory<byte>>(bytes => bytes.ToArray().SequenceEqual(serializedBytes)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_WithNonExistentKey_ReturnsDefault()
    {
        // Arrange
        var key = "non-existent-key";
        var expectedRedisKey = new RedisKey("test:non-existent-key");

        _mockDatabase.Setup(x => x.StringGetAsync(expectedRedisKey, CommandFlags.None))
            .ReturnsAsync(RedisValue.Null);

        // Act
        var result = await _cacheService.GetAsync<TestObject>(key);

        // Assert
        Assert.Null(result);

        _mockDatabase.Verify(x => x.StringGetAsync(expectedRedisKey, CommandFlags.None), Times.Once);
        _mockSerializer.Verify(x => x.DeserializeAsync<TestObject>(
            It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAsync_WithEmptyValue_ReturnsDefault()
    {
        // Arrange
        var key = "empty-key";
        var expectedRedisKey = new RedisKey("test:empty-key");

        _mockDatabase.Setup(x => x.StringGetAsync(expectedRedisKey, CommandFlags.None))
            .ReturnsAsync((RedisValue)Array.Empty<byte>());

        // Act
        var result = await _cacheService.GetAsync<TestObject>(key);

        // Assert
        Assert.Null(result);

        _mockDatabase.Verify(x => x.StringGetAsync(expectedRedisKey, CommandFlags.None), Times.Once);
        _mockSerializer.Verify(x => x.DeserializeAsync<TestObject>(
            It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAsync_WithNullOrWhitespaceKey_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _cacheService.GetAsync<TestObject>(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => _cacheService.GetAsync<TestObject>(""));
        await Assert.ThrowsAsync<ArgumentException>(() => _cacheService.GetAsync<TestObject>("   "));
    }

    [Fact]
    public async Task GetAsync_WithCancellationToken_PassesToSerializer()
    {
        // Arrange
        var key = "test-key";
        var expectedRedisKey = new RedisKey("test:test-key");
        var serializedBytes = new byte[] { 1, 2, 3, 4 };
        using var cts = new CancellationTokenSource();

        _mockDatabase.Setup(x => x.StringGetAsync(expectedRedisKey, CommandFlags.None))
            .ReturnsAsync((RedisValue)serializedBytes);

        _mockSerializer.Setup(x => x.DeserializeAsync<TestObject>(
            It.IsAny<ReadOnlyMemory<byte>>(), cts.Token))
            .ReturnsAsync(new TestObject());

        // Act
        await _cacheService.GetAsync<TestObject>(key, cts.Token);

        // Assert
        _mockSerializer.Verify(x => x.DeserializeAsync<TestObject>(
            It.IsAny<ReadOnlyMemory<byte>>(), cts.Token), Times.Once);
    }

    [Fact]
    public async Task GetAsync_WithNoKeyPrefix_UsesCorrectRedisKey()
    {
        // Arrange
        var key = "test-key";
        var expectedRedisKey = new RedisKey("test-key");
        _redisOptions.KeyPrefix = null;

        _mockDatabase.Setup(x => x.StringGetAsync(expectedRedisKey, CommandFlags.None))
            .ReturnsAsync(RedisValue.Null);

        // Act
        await _cacheService.GetAsync<TestObject>(key);

        // Assert
        _mockDatabase.Verify(x => x.StringGetAsync(expectedRedisKey, CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task GetAsync_WithEmptyKeyPrefix_UsesCorrectRedisKey()
    {
        // Arrange
        var key = "test-key";
        var expectedRedisKey = new RedisKey("test-key");
        _redisOptions.KeyPrefix = "";

        _mockDatabase.Setup(x => x.StringGetAsync(expectedRedisKey, CommandFlags.None))
            .ReturnsAsync(RedisValue.Null);

        // Act
        await _cacheService.GetAsync<TestObject>(key);

        // Assert
        _mockDatabase.Verify(x => x.StringGetAsync(expectedRedisKey, CommandFlags.None), Times.Once);
    }

    #endregion

    #region SetAsync Tests

    [Fact]
    public async Task SetAsync_WithValidKeyAndValue_SetsValueWithDefaultExpiration()
    {
        // Arrange
        var key = "test-key";
        var value = new TestObject { Id = 1, Name = "Test" };
        var expectedRedisKey = new RedisKey("test:test-key");
        var serializedBytes = new byte[] { 1, 2, 3, 4 };

        _mockSerializer.Setup(x => x.SerializeAsync(value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(serializedBytes);

        _mockDatabase.Setup(x => x.StringSetAsync(
            expectedRedisKey, serializedBytes, _redisOptions.DefaultExpiration, false, When.Always, CommandFlags.None))
            .ReturnsAsync(true);

        // Act
        await _cacheService.SetAsync(key, value);

        // Assert
        _mockSerializer.Verify(x => x.SerializeAsync(value, It.IsAny<CancellationToken>()), Times.Once);
        _mockDatabase.Verify(x => x.StringSetAsync(
            expectedRedisKey, serializedBytes, _redisOptions.DefaultExpiration, false, When.Always, CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task SetAsync_WithAbsoluteExpiration_SetsValueWithSpecifiedExpiration()
    {
        // Arrange
        var key = "test-key";
        var value = new TestObject { Id = 1, Name = "Test" };
        var expiration = TimeSpan.FromMinutes(10);
        var expectedRedisKey = new RedisKey("test:test-key");
        var serializedBytes = new byte[] { 1, 2, 3, 4 };

        _mockSerializer.Setup(x => x.SerializeAsync(value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(serializedBytes);

        _mockDatabase.Setup(x => x.StringSetAsync(
            expectedRedisKey, serializedBytes, expiration, false, When.Always, CommandFlags.None))
            .ReturnsAsync(true);

        // Act
        await _cacheService.SetAsync(key, value, expiration);

        // Assert
        _mockDatabase.Verify(x => x.StringSetAsync(
            expectedRedisKey, serializedBytes, expiration, false, When.Always, CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task SetAsync_WithNullValue_SerializesNullValue()
    {
        // Arrange
        var key = "test-key";
        TestObject? value = null;
        var expectedRedisKey = new RedisKey("test:test-key");
        var serializedBytes = new byte[] { 1, 2, 3, 4 };

        _mockSerializer.Setup(x => x.SerializeAsync(value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(serializedBytes);

        _mockDatabase.Setup(x => x.StringSetAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), false, When.Always, CommandFlags.None))
            .ReturnsAsync(true);

        // Act
        await _cacheService.SetAsync(key, value);

        // Assert
        _mockSerializer.Verify(x => x.SerializeAsync(value, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetAsync_WithNullOrWhitespaceKey_ThrowsArgumentException()
    {
        // Arrange
        var value = new TestObject();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _cacheService.SetAsync(null!, value));
        await Assert.ThrowsAsync<ArgumentException>(() => _cacheService.SetAsync("", value));
        await Assert.ThrowsAsync<ArgumentException>(() => _cacheService.SetAsync("   ", value));
    }

    [Fact]
    public async Task SetAsync_WithCancellationToken_PassesToSerializer()
    {
        // Arrange
        var key = "test-key";
        var value = new TestObject();
        var serializedBytes = new byte[] { 1, 2, 3, 4 };
        using var cts = new CancellationTokenSource();

        _mockSerializer.Setup(x => x.SerializeAsync(value, cts.Token))
            .ReturnsAsync(serializedBytes);

        _mockDatabase.Setup(x => x.StringSetAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), false, When.Always, CommandFlags.None))
            .ReturnsAsync(true);

        // Act
        await _cacheService.SetAsync(key, value, cancellationToken: cts.Token);

        // Assert
        _mockSerializer.Verify(x => x.SerializeAsync(value, cts.Token), Times.Once);
    }

    [Fact]
    public async Task SetAsync_WithNoKeyPrefix_UsesCorrectRedisKey()
    {
        // Arrange
        var key = "test-key";
        var value = new TestObject();
        var expectedRedisKey = new RedisKey("test-key");
        var serializedBytes = new byte[] { 1, 2, 3, 4 };
        _redisOptions.KeyPrefix = null;

        _mockSerializer.Setup(x => x.SerializeAsync(value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(serializedBytes);

        _mockDatabase.Setup(x => x.StringSetAsync(
            expectedRedisKey, It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), false, When.Always, CommandFlags.None))
            .ReturnsAsync(true);

        // Act
        await _cacheService.SetAsync(key, value);

        // Assert
        _mockDatabase.Verify(x => x.StringSetAsync(
            expectedRedisKey, It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), false, When.Always, CommandFlags.None), Times.Once);
    }

    #endregion

    #region RemoveAsync Tests

    [Fact]
    public async Task RemoveAsync_WithValidKey_ReturnsTrue()
    {
        // Arrange
        var key = "test-key";
        var expectedRedisKey = new RedisKey("test:test-key");

        _mockDatabase.Setup(x => x.KeyDeleteAsync(expectedRedisKey, CommandFlags.None))
            .ReturnsAsync(true);

        // Act
        var result = await _cacheService.RemoveAsync(key);

        // Assert
        Assert.True(result);
        _mockDatabase.Verify(x => x.KeyDeleteAsync(expectedRedisKey, CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_WithNonExistentKey_ReturnsFalse()
    {
        // Arrange
        var key = "non-existent-key";
        var expectedRedisKey = new RedisKey("test:non-existent-key");

        _mockDatabase.Setup(x => x.KeyDeleteAsync(expectedRedisKey, CommandFlags.None))
            .ReturnsAsync(false);

        // Act
        var result = await _cacheService.RemoveAsync(key);

        // Assert
        Assert.False(result);
        _mockDatabase.Verify(x => x.KeyDeleteAsync(expectedRedisKey, CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_WithNullOrWhitespaceKey_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _cacheService.RemoveAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => _cacheService.RemoveAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => _cacheService.RemoveAsync("   "));
    }

    [Fact]
    public async Task RemoveAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var key = "test-key";
        var expectedRedisKey = new RedisKey("test:test-key");
        using var cts = new CancellationTokenSource();

        _mockDatabase.Setup(x => x.KeyDeleteAsync(expectedRedisKey, CommandFlags.None))
            .ReturnsAsync(true);

        // Act
        var result = await _cacheService.RemoveAsync(key, cts.Token);

        // Assert
        Assert.True(result);
        _mockDatabase.Verify(x => x.KeyDeleteAsync(expectedRedisKey, CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_WithNoKeyPrefix_UsesCorrectRedisKey()
    {
        // Arrange
        var key = "test-key";
        var expectedRedisKey = new RedisKey("test-key");
        _redisOptions.KeyPrefix = null;

        _mockDatabase.Setup(x => x.KeyDeleteAsync(expectedRedisKey, CommandFlags.None))
            .ReturnsAsync(true);

        // Act
        await _cacheService.RemoveAsync(key);

        // Assert
        _mockDatabase.Verify(x => x.KeyDeleteAsync(expectedRedisKey, CommandFlags.None), Times.Once);
    }

    #endregion

    #region Key Building Tests

    [Theory]
    [InlineData("simple", "test:simple")]
    [InlineData("with-dashes", "test:with-dashes")]
    [InlineData("with:colons", "test:with:colons")]
    [InlineData("with.dots", "test:with.dots")]
    [InlineData("with_underscores", "test:with_underscores")]
    [InlineData("with/slashes", "test:with/slashes")]
    public async Task BuildKey_WithDifferentKeyFormats_HandlesCorrectly(string key, string expectedRedisKey)
    {
        // Arrange
        var redisKey = new RedisKey(expectedRedisKey);

        _mockDatabase.Setup(x => x.KeyDeleteAsync(redisKey, CommandFlags.None))
            .ReturnsAsync(true);

        // Act
        await _cacheService.RemoveAsync(key);

        // Assert
        _mockDatabase.Verify(x => x.KeyDeleteAsync(redisKey, CommandFlags.None), Times.Once);
    }

    #endregion

    #region Integration/Workflow Tests

    [Fact]
    public async Task SetAsync_GetAsync_RemoveAsync_FullWorkflow_WorksCorrectly()
    {
        // Arrange
        var key = "workflow-test";
        var value = new TestObject { Id = 42, Name = "Workflow Test" };
        var expectedRedisKey = new RedisKey("test:workflow-test");
        var serializedBytes = new byte[] { 1, 2, 3, 4 };

        // Setup serializer
        _mockSerializer.Setup(x => x.SerializeAsync(value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(serializedBytes);
        _mockSerializer.Setup(x => x.DeserializeAsync<TestObject>(
            It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value);

        // Setup database operations
        _mockDatabase.Setup(x => x.StringSetAsync(
            expectedRedisKey, It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), false, When.Always, CommandFlags.None))
            .ReturnsAsync(true);
        _mockDatabase.Setup(x => x.StringGetAsync(expectedRedisKey, CommandFlags.None))
            .ReturnsAsync((RedisValue)serializedBytes);
        _mockDatabase.Setup(x => x.KeyDeleteAsync(expectedRedisKey, CommandFlags.None))
            .ReturnsAsync(true);

        // Act
        await _cacheService.SetAsync(key, value);
        var retrievedValue = await _cacheService.GetAsync<TestObject>(key);
        var removed = await _cacheService.RemoveAsync(key);

        // Assert
        Assert.NotNull(retrievedValue);
        Assert.Equal(value.Id, retrievedValue.Id);
        Assert.Equal(value.Name, retrievedValue.Name);
        Assert.True(removed);

        // Verify all operations were called
        _mockDatabase.Verify(x => x.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), false, When.Always, CommandFlags.None), Times.Once);
        _mockDatabase.Verify(x => x.StringGetAsync(It.IsAny<RedisKey>(), CommandFlags.None), Times.Once);
        _mockDatabase.Verify(x => x.KeyDeleteAsync(It.IsAny<RedisKey>(), CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task GetAsync_WithStringType_WorksCorrectly()
    {
        // Arrange
        var key = "string-test";
        var value = "Hello, World!";
        var expectedRedisKey = new RedisKey("test:string-test");
        var serializedBytes = System.Text.Encoding.UTF8.GetBytes($"\"{value}\"");

        _mockDatabase.Setup(x => x.StringGetAsync(expectedRedisKey, CommandFlags.None))
            .ReturnsAsync((RedisValue)serializedBytes);

        _mockSerializer.Setup(x => x.DeserializeAsync<string>(
            It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value);

        // Act
        var result = await _cacheService.GetAsync<string>(key);

        // Assert
        Assert.Equal(value, result);
    }

    [Fact]
    public async Task GetAsync_WithPrimitiveType_WorksCorrectly()
    {
        // Arrange
        var key = "int-test";
        var value = 42;
        var expectedRedisKey = new RedisKey("test:int-test");
        var serializedBytes = System.Text.Encoding.UTF8.GetBytes(value.ToString());

        _mockDatabase.Setup(x => x.StringGetAsync(expectedRedisKey, CommandFlags.None))
            .ReturnsAsync((RedisValue)serializedBytes);

        _mockSerializer.Setup(x => x.DeserializeAsync<int>(
            It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value);

        // Act
        var result = await _cacheService.GetAsync<int>(key);

        // Assert
        Assert.Equal(value, result);
    }

    #endregion

    private class TestObject
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }
}