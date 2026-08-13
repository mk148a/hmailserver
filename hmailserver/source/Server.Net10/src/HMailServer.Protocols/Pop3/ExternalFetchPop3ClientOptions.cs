namespace HMailServer.Protocols.Pop3;

public sealed record ExternalFetchPop3ClientOptions
{
    public TimeSpan OperationTimeout { get; init; } = TimeSpan.FromSeconds(900);

    public int ReceiveBufferBytes { get; init; } = 64 * 1024;

    public int SendBufferBytes { get; init; } = 64 * 1024;

    public bool NoDelay { get; init; } = true;

    public bool EnforceEgressPolicy { get; init; } = true;

    public IReadOnlyList<string> AllowedPrivateCidrs { get; init; } = [];
}
