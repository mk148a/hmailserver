using System.Net;

namespace HMailServer.Protocols.Imap;

public sealed record ImapTcpListenerOptions
{
    public bool Enabled { get; init; }

    public IPAddress ListenAddress { get; init; } = IPAddress.Any;

    public int Port { get; init; } = 143;

    public int Backlog { get; init; } = 512;

    public int MaxConcurrentConnections { get; init; } = 1000;

    public bool NoDelay { get; init; } = true;

    public int ReceiveBufferBytes { get; init; } = 64 * 1024;

    public int SendBufferBytes { get; init; } = 64 * 1024;

    public TimeSpan ShutdownGracePeriod { get; init; } = TimeSpan.FromSeconds(10);
}
