namespace HMailServer.Core.Abstractions;

public interface ILegacyBlowfishCipher
{
    string Encrypt(string input);

    bool TryDecrypt(string input, out string output);
}
