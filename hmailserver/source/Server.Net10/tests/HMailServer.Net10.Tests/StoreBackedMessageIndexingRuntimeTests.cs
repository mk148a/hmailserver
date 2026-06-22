using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class StoreBackedMessageIndexingRuntimeTests
{
    [TestMethod]
    public void Runtime_MapsStatusAndCommandsToAdministrationStore()
    {
        var store = new RecordingAdministrationStore
        {
            Status = new MessageIndexingAdministrationStatus(
                TotalMessageCount: 12,
                TotalIndexedCount: 9,
                Enabled: false,
                IsFullTextReady: true,
                QueuedMessageCount: 3,
                LastError: "last error")
        };
        var runtime = new StoreBackedMessageIndexingRuntime(store);

        Assert.AreEqual(12, runtime.TotalMessageCount);
        Assert.AreEqual(9, runtime.TotalIndexedCount);
        Assert.IsFalse(runtime.Enabled);
        runtime.Enabled = true;
        runtime.Clear();
        runtime.Index();
        Assert.AreEqual("SqlServerFullText", runtime.Backend);
        Assert.IsTrue(runtime.IsFullTextReady);
        Assert.AreEqual("Queued=3", runtime.BackfillStatus);
        Assert.AreEqual("last error", runtime.LastError);
        runtime.Rebuild();

        Assert.AreEqual(true, store.LastEnabledValue);
        Assert.AreEqual(1, store.ClearCalls);
        Assert.AreEqual(1, store.IndexCalls);
        Assert.AreEqual(1, store.RebuildCalls);
    }

    private sealed class RecordingAdministrationStore : IMessageIndexingAdministrationStore
    {
        public required MessageIndexingAdministrationStatus Status { get; init; }

        public bool? LastEnabledValue { get; private set; }

        public int ClearCalls { get; private set; }

        public int IndexCalls { get; private set; }

        public int RebuildCalls { get; private set; }

        public ValueTask<MessageIndexingAdministrationStatus> GetStatusAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Status);

        public ValueTask<bool> IsEnabledAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(Status.Enabled);

        public ValueTask SetEnabledAsync(bool enabled, CancellationToken cancellationToken)
        {
            LastEnabledValue = enabled;
            return ValueTask.CompletedTask;
        }

        public ValueTask ClearAsync(CancellationToken cancellationToken)
        {
            ClearCalls++;
            return ValueTask.CompletedTask;
        }

        public ValueTask IndexAsync(CancellationToken cancellationToken)
        {
            IndexCalls++;
            return ValueTask.CompletedTask;
        }

        public ValueTask RebuildAsync(CancellationToken cancellationToken)
        {
            RebuildCalls++;
            return ValueTask.CompletedTask;
        }
    }
}
