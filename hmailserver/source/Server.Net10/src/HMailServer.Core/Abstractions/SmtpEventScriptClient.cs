namespace HMailServer.Core.Abstractions;

public sealed record SmtpEventScriptClient(
    string Username,
    string IPAddress,
    int Port,
    long SessionId,
    string HeloHost,
    bool IsAuthenticated,
    bool IsEncryptedConnection,
    string CipherVersion = "",
    string CipherName = "",
    int CipherBits = 0);
