using System.Security.Cryptography;

namespace SeoInsight.Web.Infrastructure.Data;

/// <summary>
/// Static utility class for generating time-ordered UUIDv7 and common DB formatting.
/// </summary>
public static class DbConstants
{
    /// <summary>
    /// Generates a time-ordered UUIDv7 (RFC 9562) in C#.
    /// 48-bit timestamp in high bits ensures optimal sequential B-Tree performance in PostgreSQL.
    /// </summary>
    public static Guid NewUuidV7()
    {
        // 48-bit timestamp in milliseconds
        long unixTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        byte[] bytes = new byte[16];

        // Fill with cryptographically strong random bytes
        RandomNumberGenerator.Fill(bytes);

        // Put timestamp in big-endian into first 6 bytes
        bytes[0] = (byte)(unixTimeMs >> 40);
        bytes[1] = (byte)(unixTimeMs >> 32);
        bytes[2] = (byte)(unixTimeMs >> 24);
        bytes[3] = (byte)(unixTimeMs >> 16);
        bytes[4] = (byte)(unixTimeMs >> 8);
        bytes[5] = (byte)unixTimeMs;

        // Set version 7 (0111xxxx in byte 6)
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x70);

        // Set variant 10xx (RFC 4122 in byte 8)
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes);
    }
}
