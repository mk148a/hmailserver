using HMailServer.Core.Abstractions;

namespace HMailServer.Security;

public sealed class LegacyServerAdministratorAuthenticationProvider : IServerAdministratorAuthenticationProvider
{
    private const int Md5HashLength = 32;
    private const int SaltedSha256HashLength = 70;

    private readonly string _storedPasswordHash;

    public LegacyServerAdministratorAuthenticationProvider(string storedPasswordHash)
    {
        ArgumentNullException.ThrowIfNull(storedPasswordHash);
        _storedPasswordHash = storedPasswordHash.Trim();
    }

    public bool Authenticate(string username, string password)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);

        if (!username.Equals("Administrator", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (_storedPasswordHash.Length == 0)
        {
            return password.Length == 0;
        }

        var encryptionType = _storedPasswordHash.Length switch
        {
            Md5HashLength => LegacyPasswordEncryptionType.MD5,
            SaltedSha256HashLength => LegacyPasswordEncryptionType.SHA256,
            _ => (LegacyPasswordEncryptionType?)null
        };

        return encryptionType is { } type
            && LegacyPasswordVerifier.Verify(password, _storedPasswordHash, type);
    }
}
