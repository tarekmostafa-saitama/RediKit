using System.Text;
using RediKit.Serialization;

namespace RediKit.UnitTests.Serialization;

public class CacheSerializerTests
{
    private readonly CacheSerializer _serializer = new();

    [Fact]
    public async Task SerializeAsync_WithValidObject_ReturnsSerializedBytes()
    {
        // Arrange
        var testObject = new { Name = "Test", Value = 42 };

        // Act
        var result = await _serializer.SerializeAsync(testObject);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task SerializeAsync_WithNull_ReturnsSerializedNullBytes()
    {
        // Arrange
        object? nullObject = null;

        // Act
        var result = await _serializer.SerializeAsync(nullObject);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task SerializeAsync_WithString_ReturnsSerializedStringBytes()
    {
        // Arrange
        var testString = "Hello, World!";

        // Act
        var result = await _serializer.SerializeAsync(testString);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        // Verify it's valid JSON by checking it contains quotes
        var json = Encoding.UTF8.GetString(result);
        Assert.Contains("\"", json);
    }

    [Fact]
    public async Task SerializeAsync_WithComplexObject_ReturnsSerializedBytes()
    {
        // Arrange
        var complexObject = new TestClass
        {
            Id = 1,
            Name = "Test",
            Items = new List<string> { "Item1", "Item2" },
            Metadata = new Dictionary<string, object> { { "key", "value" } }
        };

        // Act
        var result = await _serializer.SerializeAsync(complexObject);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task SerializeAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var testObject = new { Data = "test" };
        using var cts = new CancellationTokenSource();

        // Act
        var result = await _serializer.SerializeAsync(testObject, cts.Token);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task DeserializeAsync_WithValidBytes_ReturnsDeserializedObject()
    {
        // Arrange
        var originalObject = new { Name = "Test", Value = 42 };
        var serializedBytes = await _serializer.SerializeAsync(originalObject);

        // Act
        var result = await _serializer.DeserializeAsync<dynamic>(serializedBytes);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task DeserializeAsync_WithEmptyMemory_ReturnsDefault()
    {
        // Arrange
        var emptyMemory = ReadOnlyMemory<byte>.Empty;

        // Act
        var result = await _serializer.DeserializeAsync<string>(emptyMemory);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DeserializeAsync_WithValidStringBytes_ReturnsString()
    {
        // Arrange
        var originalString = "Hello, World!";
        var serializedBytes = await _serializer.SerializeAsync(originalString);

        // Act
        var result = await _serializer.DeserializeAsync<string>(serializedBytes);

        // Assert
        Assert.Equal(originalString, result);
    }

    [Fact]
    public async Task DeserializeAsync_WithComplexObjectBytes_ReturnsComplexObject()
    {
        // Arrange
        var originalObject = new TestClass
        {
            Id = 1,
            Name = "Test",
            Items = new List<string> { "Item1", "Item2" },
            Metadata = new Dictionary<string, object> { { "key", "value" } }
        };
        var serializedBytes = await _serializer.SerializeAsync(originalObject);

        // Act
        var result = await _serializer.DeserializeAsync<TestClass>(serializedBytes);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(originalObject.Id, result.Id);
        Assert.Equal(originalObject.Name, result.Name);
        Assert.Equal(originalObject.Items.Count, result.Items?.Count);
    }

    [Fact]
    public async Task DeserializeAsync_WithNullBytes_ReturnsDefault()
    {
        // Arrange
        var nullBytes = await _serializer.SerializeAsync<object?>(null);

        // Act
        var result = await _serializer.DeserializeAsync<object>(nullBytes);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DeserializeAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var originalObject = new { Data = "test" };
        var serializedBytes = await _serializer.SerializeAsync(originalObject);
        using var cts = new CancellationTokenSource();

        // Act
        var result = await _serializer.DeserializeAsync<dynamic>(serializedBytes, cts.Token);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task SerializeAsync_DeserializeAsync_RoundTrip_PreservesData()
    {
        // Arrange
        var originalObject = new TestClass
        {
            Id = 123,
            Name = "RoundTrip Test",
            Items = new List<string> { "A", "B", "C" },
            Metadata = new Dictionary<string, object>
            {
                { "timestamp", DateTime.UtcNow.ToString() },
                { "version", 1.0 }
            }
        };

        // Act
        var serializedBytes = await _serializer.SerializeAsync(originalObject);
        var deserializedObject = await _serializer.DeserializeAsync<TestClass>(serializedBytes);

        // Assert
        Assert.NotNull(deserializedObject);
        Assert.Equal(originalObject.Id, deserializedObject.Id);
        Assert.Equal(originalObject.Name, deserializedObject.Name);
        Assert.Equal(originalObject.Items?.Count, deserializedObject.Items?.Count);
        Assert.Equal(originalObject.Metadata?.Count, deserializedObject.Metadata?.Count);
    }

    [Fact]
    public async Task DeserializeAsync_WithInvalidJson_ThrowsJsonException()
    {
        // Arrange
        var invalidJsonBytes = "{ invalid json }"u8.ToArray();

        // Act & Assert
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(
            () => _serializer.DeserializeAsync<object>(invalidJsonBytes));
    }

    private class TestClass
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public List<string>? Items { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }
}