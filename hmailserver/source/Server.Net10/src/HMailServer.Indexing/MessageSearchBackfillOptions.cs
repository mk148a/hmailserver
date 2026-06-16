namespace HMailServer.Indexing;

public sealed record MessageSearchBackfillOptions(
    string LeaseOwner,
    int BatchSize,
    TimeSpan LeaseDuration,
    TimeSpan RetryDelay,
    int MaxAttempts)
{
    public static MessageSearchBackfillOptions Default(string leaseOwner)
    {
        return new MessageSearchBackfillOptions(
            leaseOwner,
            BatchSize: 128,
            LeaseDuration: TimeSpan.FromMinutes(5),
            RetryDelay: TimeSpan.FromMinutes(2),
            MaxAttempts: 10);
    }
}
