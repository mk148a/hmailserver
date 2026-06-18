namespace HMailServer.Protocols.Pop3;

public sealed record ExternalFetchProcessorOptions(
    int BatchSize,
    int MaxMessagesPerAccount)
{
    public static ExternalFetchProcessorOptions Default { get; } =
        new(
            BatchSize: 10,
            MaxMessagesPerAccount: 100);
}
