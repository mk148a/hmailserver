namespace HMailServer.Core.Abstractions;

public sealed record ServerStatusRuntimeCounters(
    int ServerState,
    string StartTime,
    int ProcessedMessages,
    int RemovedViruses,
    int RemovedSpamMessages,
    IReadOnlyDictionary<int, int> SessionCounts,
    int ThreadID);
