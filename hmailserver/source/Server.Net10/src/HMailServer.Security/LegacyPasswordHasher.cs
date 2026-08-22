using System.Security.Cryptography;
using System.Text;

namespace HMailServer.Security;

public static class LegacyPasswordHasher
{
    private const int Sha256SaltLength = 6;

    public static string CreateSaltedSha256(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        Span<byte> saltBytes = stackalloc byte[Sha256SaltLength / 2];
        RandomNumberGenerator.Fill(saltBytes);
        var salt = Convert.ToHexString(saltBytes).ToLowerInvariant();
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(salt + password));

        return salt + Convert.ToHexString(digest).ToLowerInvariant();
    }
}
