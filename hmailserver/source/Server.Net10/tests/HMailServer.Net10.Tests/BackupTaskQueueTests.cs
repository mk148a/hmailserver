using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class BackupTaskQueueTests
{
    [TestMethod]
    public async Task EnqueuePublishesOneRequestToTheMaintenanceReader()
    {
        using var queue = new BackupTaskQueue();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var request = CreateRequest();

        Assert.IsTrue(queue.TryEnqueue(request));

        await using var reader = queue
            .ReadAllAsync(cancellation.Token)
            .GetAsyncEnumerator(cancellation.Token);

        Assert.IsTrue(await reader.MoveNextAsync());
        Assert.AreSame(request, reader.Current);
    }

    [TestMethod]
    public void DisposeCompletesTheQueueAndRejectsNewRequests()
    {
        var queue = new BackupTaskQueue();
        queue.Dispose();

        Assert.IsFalse(queue.TryEnqueue(CreateRequest()));
    }

    private static BackupTaskRequest CreateRequest() => new(
        static _ => ValueTask.CompletedTask,
        static _ => { },
        static _ => { },
        static () => { },
        static () => { });
}
