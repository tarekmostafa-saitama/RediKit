namespace RediKit.Serialization;

/// <summary>
/// Interface for cache value serialization.
/// </summary>
public interface ICacheSerializer
{
    Task<byte[]> SerializeAsync<T>(T value, CancellationToken cancellationToken = default);
    Task<T?> DeserializeAsync<T>(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);
}