namespace HMailServer.Security;

public sealed record ClamAvInstreamClientOptions
{
    public string Host { get; init; } = "127.0.0.1";

    public int Port { get; init; } = 3310;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    public int ChunkSize { get; init; } = 64 * 1024;
}
