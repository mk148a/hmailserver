namespace HMailServer.Security;

public sealed record SpamAssassinClientOptions
{
    public string Host { get; init; } = "127.0.0.1";

    public int Port { get; init; } = 783;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    public int MaxResponseHeaderBytes { get; init; } = 16 * 1024;

    public int MaxResponseBytes { get; init; } = 100 * 1024 * 1024;
}
