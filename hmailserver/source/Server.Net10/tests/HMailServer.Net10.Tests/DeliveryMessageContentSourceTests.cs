using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class DeliveryMessageContentSourceTests
{
    [TestMethod]
    public async Task TryDeleteAsync_DeletesQueuedMessageInsideDataDirectory()
    {
        var dataDirectory = CreateTemporaryDirectory();
        try
        {
            var messagePath = Path.Combine(dataDirectory, "queue.eml");
            await File.WriteAllTextAsync(messagePath, "Subject: Delete\r\n\r\nBody\r\n");
            var source = CreateSource(dataDirectory);

            var deleted = await source.TryDeleteAsync(
                CreateMessage("queue.eml"),
                CancellationToken.None);

            Assert.IsTrue(deleted);
            Assert.IsFalse(File.Exists(messagePath));
        }
        finally
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task TryDeleteAsync_RejectsPathOutsideDataDirectory()
    {
        var container = CreateTemporaryDirectory();
        var dataDirectory = Path.Combine(container, "data");
        Directory.CreateDirectory(dataDirectory);
        var outsidePath = Path.Combine(container, "outside.eml");
        await File.WriteAllTextAsync(outsidePath, "Subject: Keep\r\n\r\nBody\r\n");
        try
        {
            var source = CreateSource(dataDirectory);

            var deleted = await source.TryDeleteAsync(
                CreateMessage(outsidePath),
                CancellationToken.None);

            Assert.IsFalse(deleted);
            Assert.IsTrue(File.Exists(outsidePath));
        }
        finally
        {
            Directory.Delete(container, recursive: true);
        }
    }

    [TestMethod]
    public async Task TryDeleteAsync_HonorsCancellationBeforeDeleting()
    {
        var dataDirectory = CreateTemporaryDirectory();
        try
        {
            var messagePath = Path.Combine(dataDirectory, "queue.eml");
            await File.WriteAllTextAsync(messagePath, "Subject: Keep\r\n\r\nBody\r\n");
            var source = CreateSource(dataDirectory);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => source.TryDeleteAsync(
                    CreateMessage("queue.eml"),
                    cancellation.Token).AsTask());

            Assert.IsTrue(File.Exists(messagePath));
        }
        finally
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    private static DeliveryMessageContentSource CreateSource(string dataDirectory) =>
        new(new MessageFilePathResolver(
            new MessageFileSearchDocumentSourceOptions(dataDirectory)));

    private static DeliveryQueuedMessage CreateMessage(string fileName) =>
        new(
            new MessageIdentity(1, 0, 0, 0),
            fileName,
            "sender@example.test",
            Size: 32,
            DateTimeOffset.UtcNow,
            Flags: 0,
            CurrentRetryCount: 0,
            Recipients: []);

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "hmailserver-net10-delivery-content-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
