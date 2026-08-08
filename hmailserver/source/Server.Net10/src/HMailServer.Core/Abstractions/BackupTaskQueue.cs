using System.Threading.Channels;

namespace HMailServer.Core.Abstractions;

public sealed class BackupTaskRequest
{
    public BackupTaskRequest(
        Func<CancellationToken, ValueTask> executeAsync,
        Action<string> setStatus,
        Action<string> failed,
        Action completed,
        Action threadStopped,
        Action? abort = null)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        ArgumentNullException.ThrowIfNull(setStatus);
        ArgumentNullException.ThrowIfNull(failed);
        ArgumentNullException.ThrowIfNull(completed);
        ArgumentNullException.ThrowIfNull(threadStopped);

        ExecuteAsync = executeAsync;
        SetStatus = setStatus;
        Failed = failed;
        Completed = completed;
        ThreadStopped = threadStopped;
        AbortCallback = abort;
    }

    public Func<CancellationToken, ValueTask> ExecuteAsync { get; }

    public Action<string> SetStatus { get; }

    public Action<string> Failed { get; }

    public Action Completed { get; }

    public Action ThreadStopped { get; }

    public Action? AbortCallback { get; }

    private int _threadStopped;
    private int _aborted;

    public void AbortPending()
    {
        if (Interlocked.Exchange(ref _aborted, 1) != 0)
        {
            return;
        }

        try
        {
            AbortCallback?.Invoke();
        }
        finally
        {
            NotifyThreadStopped();
        }
    }

    public void NotifyThreadStopped()
    {
        if (Interlocked.Exchange(ref _threadStopped, 1) == 0)
        {
            ThreadStopped();
        }
    }
}

public interface IBackupTaskQueue
{
    bool TryEnqueue(BackupTaskRequest request);

    IAsyncEnumerable<BackupTaskRequest> ReadAllAsync(CancellationToken cancellationToken);

    void CompleteAndAbortPending();
}

public sealed class BackupTaskQueue : IBackupTaskQueue, IDisposable
{
    private readonly Channel<BackupTaskRequest> _queue =
        Channel.CreateUnbounded<BackupTaskRequest>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

    public bool TryEnqueue(BackupTaskRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _queue.Writer.TryWrite(request);
    }

    public IAsyncEnumerable<BackupTaskRequest> ReadAllAsync(CancellationToken cancellationToken) =>
        _queue.Reader.ReadAllAsync(cancellationToken);

    public void CompleteAndAbortPending()
    {
        _queue.Writer.TryComplete();
        while (_queue.Reader.TryRead(out var request))
        {
            request.AbortPending();
        }
    }

    public void Dispose() => CompleteAndAbortPending();
}
