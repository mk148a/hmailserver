namespace HMailServer.Protocols.Pop3;

public sealed record Pop3SessionConnectionContext(
    string ClientIPAddress = "",
    int ClientPort = 0,
    long SessionId = 0,
    bool IsEncryptedConnection = false)
{
    public static Pop3SessionConnectionContext Empty { get; } = new();
}
