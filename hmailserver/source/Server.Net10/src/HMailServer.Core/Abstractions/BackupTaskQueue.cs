using System.Threading.Channels;

namespace HMailServer.Core.Abstractions;

public sealed class BackupTaskRequest
{
    public BackupTaskRequest(
        Func<CancellationToken, ValueTask> executeAsync,
        Action<string> setStatus,
        Action<string> failed,
        Action completed,
        Action threadStopped)
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
    }

    public Func<CancellationToken, ValueTask> ExecuteAsync { get; }

    public Action<string> SetStatus { get; }

    public Action<string> Failed { get; }

    public Action Completed { get; }

    public Action ThreadStopped { get; }
}

public interface IBackupTaskQueue
{
    bool TryEnqueue(BackupTaskRequest request);

    IAsyncEnumerable<BackupTaskRequest> ReadAllAsync(CancellationToken cancellationToken);
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

    public void Dispose() => _queue.Writer.TryComplete();
}
