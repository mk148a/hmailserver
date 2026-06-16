using System.Net;

namespace HMailServer.Protocols.Smtp;

public sealed record SmtpTcpListenerOptions
{
    public bool Enabled { get; init; }

    public IPAddress ListenAddress { get; init; } = IPAddress.Any;

    public int Port { get; init; } = 25;

    public int Backlog { get; init; } = 512;

    public int MaxConcurrentConnections { get; init; } = 1000;

    public bool NoDelay { get; init; } = true;

    public int ReceiveBufferBytes { get; init; } = 64 * 1024;

    public int SendBufferBytes { get; init; } = 64 * 1024;

    public TimeSpan ShutdownGracePeriod { get; init; } = TimeSpan.FromSeconds(10);
}
