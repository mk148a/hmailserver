using System.Runtime.CompilerServices;
using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Pop3;
using HMailServer.Service;
using Microsoft.Extensions.Logging.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ExternalFetchHostedServiceTests
{
    [TestMethod]
    public async Task IdleWorker_RunsAgainWhenSignaledAndStopsOnCancellation()
    {
        var store = new CountingExternalFetchAccountStore();
        var processor = new ExternalFetchProcessor(
            store,
            new UnusedExternalFetchSessionFactory(),
            new UnusedSmtpMessageReceiver());
        using var signal = new ExternalFetchWakeSignal();
        var service = new ExternalFetchHostedService(
            new ExternalFetchHostedServiceOptions(TimeSpan.FromSeconds(30)),
            ExternalFetchProcessorOptions.Default,
            processor,
            signal,
            NullLogger<ExternalFetchHostedService>.Instance);
        using var cancellation = new CancellationTokenSource();

        await service.StartAsync(cancellation.Token);
        await store.FirstBatch.Task.WaitAsync(TimeSpan.FromSeconds(2));

        signal.Signal();
        await store.SecondBatch.Task.WaitAsync(TimeSpan.FromSeconds(1));

        cancellation.Cancel();
        await service.StopAsync(CancellationToken.None);

        Assert.AreEqual(2, store.BatchCount);
    }

    private sealed class CountingExternalFetchAccountStore : IExternalFetchAccountStore
    {
        private int _batchCount;

        public TaskCompletionSource<bool> FirstBatch { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> SecondBatch { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int BatchCount => Volatile.Read(ref _batchCount);

        public async IAsyncEnumerable<ExternalFetchAccountLease> LeaseReadyAccountsAsync(
            int batchSize,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }

        public ValueTask<int> DeferInactiveAccountsAsync(CancellationToken cancellationToken)
        {
            var count = Interlocked.Increment(ref _batchCount);
            if (count == 1)
            {
                FirstBatch.TrySetResult(true);
            }
            else if (count == 2)
            {
                SecondBatch.TrySetResult(true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(0);
        }

        public ValueTask<bool> CompleteAsync(int fetchAccountId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<bool> ReleaseAsync(int fetchAccountId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask ResetLocksAsync(CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<ExternalFetchKnownUid>> LoadKnownUidsAsync(
            int fetchAccountId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask AddKnownUidAsync(
            int fetchAccountId,
            string uid,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<bool> DeleteKnownUidAsync(int uidId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class UnusedExternalFetchSessionFactory : IExternalFetchSessionFactory
    {
        public ValueTask<IExternalFetchSession> ConnectAsync(
            ExternalFetchAccountLease account,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class UnusedSmtpMessageReceiver : ISmtpMessageReceiver
    {
        public ValueTask<SmtpReceiveResult> ReceiveAsync(
            SmtpReceiveRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
