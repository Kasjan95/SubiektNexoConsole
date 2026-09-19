using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace SubiektNexoConnector.Infrastructure.Idempotency;

public static class IdempotencyRequestHasher
{
    public static string Compute(string method, string requestTarget, ReadOnlyMemory<byte> body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestTarget);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, Encoding.UTF8.GetBytes(method.ToUpperInvariant()));
        Append(hash, Encoding.UTF8.GetBytes(requestTarget));
        Append(hash, body.Span);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void Append(IncrementalHash hash, ReadOnlySpan<byte> value)
    {
        // Length prefixes prevent ambiguous combinations of method, target and body.
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, value.Length);
        hash.AppendData(length);
        hash.AppendData(value);
    }
}
