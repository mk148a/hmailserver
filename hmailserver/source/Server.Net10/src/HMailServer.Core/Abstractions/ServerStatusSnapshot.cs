namespace HMailServer.Core.Abstractions;

public sealed record ServerStatusSnapshot(
    string UndeliveredMessages,
    string StartTime,
    int ProcessedMessages,
    int RemovedViruses,
    int RemovedSpamMessages,
    IReadOnlyDictionary<int, int> SessionCounts,
    int ThreadID)
{
    public int GetSessionCount(int sessionType) =>
        SessionCounts.TryGetValue(sessionType, out var count)
            ? count
            : 0;
}
