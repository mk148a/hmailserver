using HMailServer.Core.Abstractions;

namespace HMailServer.Security;

public sealed class LegacyBlowfishCipherRuntime : ILegacyBlowfishCipher
{
    public string Encrypt(string input) =>
        LegacyBlowfishPasswordCipher.Encrypt(input);

    public bool TryDecrypt(string input, out string output) =>
        LegacyBlowfishPasswordCipher.TryDecrypt(input, out output);
}
