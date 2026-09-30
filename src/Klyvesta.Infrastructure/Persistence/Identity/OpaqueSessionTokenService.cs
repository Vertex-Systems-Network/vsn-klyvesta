using System.Security.Cryptography;
using System.Text;

namespace Klyvesta.Infrastructure.Persistence.Identity;

public sealed class OpaqueSessionTokenService
{
    public string CreateToken()
    {
        Span<byte> token = stackalloc byte[32];
        RandomNumberGenerator.Fill(token);
        return Convert.ToBase64String(token)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public string HashToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public bool Matches(string token, string expectedHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedHash);

        var actual = Convert.FromHexString(HashToken(token));
        var expected = Convert.FromHexString(expectedHash);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
