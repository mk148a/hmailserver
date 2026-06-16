using System.Security.Cryptography;
using System.Text;

namespace HMailServer.Security;

public static class LegacyPasswordVerifier
{
    private const int Sha256SaltLength = 6;

    public static bool Verify(
        string password,
        string storedPassword,
        LegacyPasswordEncryptionType encryptionType)
    {
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        return encryptionType switch
        {
            LegacyPasswordEncryptionType.None => VerifyPlainText(password, storedPassword),
            LegacyPasswordEncryptionType.MD5 => FixedTimeEqualsHex(ComputeHashHex(HashAlgorithmName.MD5, password), storedPassword),
            LegacyPasswordEncryptionType.SHA256 => VerifySaltedSha256(password, storedPassword),
            LegacyPasswordEncryptionType.BlowFish => VerifyBlowfish(password, storedPassword),
            _ => false
        };
    }

    private static bool VerifyPlainText(string password, string storedPassword) =>
        string.Equals(password, storedPassword, StringComparison.OrdinalIgnoreCase);

    private static bool VerifySaltedSha256(string password, string storedPassword)
    {
        if (storedPassword.Length <= Sha256SaltLength)
        {
            return false;
        }

        var salt = storedPassword[..Sha256SaltLength];
        var candidate = salt + ComputeHashHex(HashAlgorithmName.SHA256, salt + password);
        return FixedTimeEqualsHex(candidate, storedPassword);
    }

    private static bool VerifyBlowfish(string password, string storedPassword)
    {
        if (!LegacyBlowfishPasswordCipher.TryDecrypt(storedPassword, out var decrypted))
        {
            return false;
        }

        return string.Equals(password, decrypted, StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeHashHex(HashAlgorithmName algorithmName, string value)
    {
        var input = Encoding.UTF8.GetBytes(value);
        var output = algorithmName == HashAlgorithmName.MD5
            ? MD5.HashData(input)
            : SHA256.HashData(input);
        return Convert.ToHexString(output).ToLowerInvariant();
    }

    private static bool FixedTimeEqualsHex(string candidate, string stored)
    {
        if (candidate.Length != stored.Length)
        {
            return false;
        }

        var left = Encoding.ASCII.GetBytes(candidate.ToLowerInvariant());
        var right = Encoding.ASCII.GetBytes(stored.ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(left, right);
    }
}
