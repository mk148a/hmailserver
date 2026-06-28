namespace HMailServer.Core.Abstractions;

public sealed record ServerStatusRuntimeCounters(
    string StartTime,
    int ProcessedMessages,
    int RemovedViruses,
    int RemovedSpamMessages,
    IReadOnlyDictionary<int, int> SessionCounts,
    int ThreadID);
