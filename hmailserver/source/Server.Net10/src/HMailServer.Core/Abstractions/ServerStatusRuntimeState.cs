using System.Collections.Concurrent;
using System.Globalization;

namespace HMailServer.Core.Abstractions;

public sealed class ServerStatusRuntimeState
{
    private readonly DateTimeOffset _startedAt;
    private readonly ConcurrentDictionary<int, int> _sessionCounts = new();
    private int _processedMessages;
    private int _removedViruses;
    private int _removedSpamMessages;

    public ServerStatusRuntimeState()
        : this(DateTimeOffset.Now)
    {
    }

    public ServerStatusRuntimeState(DateTimeOffset startedAt)
    {
        _startedAt = startedAt;
    }

    public ServerStatusRuntimeCounters Capture() =>
        new(
            FormatLegacyLocalDateTime(_startedAt),
            Volatile.Read(ref _processedMessages),
            Volatile.Read(ref _removedViruses),
            Volatile.Read(ref _removedSpamMessages),
            _sessionCounts.ToDictionary(
                static pair => pair.Key,
                static pair => Math.Max(0, pair.Value)),
            Environment.CurrentManagedThreadId);

    public void OnMessageProcessed() => Interlocked.Increment(ref _processedMessages);

    public void OnVirusRemoved() => Interlocked.Increment(ref _removedViruses);

    public void OnSpamMessageDetected() => Interlocked.Increment(ref _removedSpamMessages);

    public IDisposable TrackSession(int sessionType)
    {
        _sessionCounts.AddOrUpdate(sessionType, 1, static (_, count) => count + 1);
        return new SessionLease(this, sessionType);
    }

    private void ReleaseSession(int sessionType) =>
        _sessionCounts.AddOrUpdate(
            sessionType,
            0,
            static (_, count) => Math.Max(0, count - 1));

    private static string FormatLegacyLocalDateTime(DateTimeOffset value) =>
        value.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private sealed class SessionLease : IDisposable
    {
        private readonly ServerStatusRuntimeState _owner;
        private readonly int _sessionType;
        private int _disposed;

        public SessionLease(ServerStatusRuntimeState owner, int sessionType)
        {
            _owner = owner;
            _sessionType = sessionType;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _owner.ReleaseSession(_sessionType);
            }
        }
    }
}
