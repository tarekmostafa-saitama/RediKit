using System.Text.Json;

namespace RediKit.Serialization;

/// <summary>
/// JSON serializer for cache values using System.Text.Json.
/// </summary>
public sealed class CacheSerializer : ICacheSerializer
{
    public async Task<byte[]> SerializeAsync<T>(T value, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync(stream, value, (JsonSerializerOptions?)null, cancellationToken);
        return stream.ToArray();
    }

    public async Task<T?> DeserializeAsync<T>(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        if (payload.IsEmpty)
        {
            return default;
        }

        using var stream = new MemoryStream(payload.ToArray());
        return await JsonSerializer.DeserializeAsync<T>(stream, (JsonSerializerOptions?)null, cancellationToken);
    }
}


