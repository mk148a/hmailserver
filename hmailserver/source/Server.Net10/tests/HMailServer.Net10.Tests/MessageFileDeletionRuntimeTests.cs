using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class MessageFileDeletionRuntimeTests
{
    [TestMethod]
    public void TryDelete_RemovesPrivatePublicAndQueueFiles()
    {
        using var fixture = new TemporaryDataDirectory();
        var resolver = new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(fixture.Path));
        var runtime = new MessageFileDeletionRuntime(resolver, retryDelay: TimeSpan.Zero);
        var messages = new[]
        {
            CreateMessage(resolver, "private.eml", 10, 20, "user@example.test"),
            CreateMessage(resolver, "public.eml", 0, 30, null),
            CreateMessage(resolver, "queue.eml", 0, 0, null)
        };

        foreach (var message in messages)
        {
            var path = resolver.Resolve(message.FileName, message.AccountId, message.FolderId, message.AccountAddress)!;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "message");
            Assert.IsTrue(runtime.TryDelete(message));
            Assert.IsFalse(File.Exists(path));
        }
    }

    [TestMethod]
    public void TryDelete_TreatsMissingFileAsSuccess()
    {
        using var fixture = new TemporaryDataDirectory();
        var resolver = new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(fixture.Path));
        var runtime = new MessageFileDeletionRuntime(resolver, retryDelay: TimeSpan.Zero);

        Assert.IsTrue(runtime.TryDelete(CreateMessage(resolver, "missing.eml", 0, 0, null)));
    }

    [TestMethod]
    public void TryDelete_RejectsOutsideAndTraversalPathsWithoutCallingFileSystem()
    {
        using var fixture = new TemporaryDataDirectory();
        var resolver = new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(fixture.Path));
        var calls = 0;
        var runtime = new MessageFileDeletionRuntime(resolver, _ =>
        {
            calls++;
            return true;
        }, TimeSpan.Zero);

        Assert.IsFalse(runtime.TryDelete(CreateMessage(resolver, "..\\outside.eml", 0, 0, null)));
        Assert.IsFalse(runtime.TryDelete(CreateMessage(resolver, Path.Combine(fixture.Path, "..", "outside.eml"), 0, 0, null)));
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public void TryDelete_RetriesFiveTimesAndReportsFailure()
    {
        using var fixture = new TemporaryDataDirectory();
        var resolver = new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(fixture.Path));
        var calls = 0;
        var runtime = new MessageFileDeletionRuntime(resolver, _ =>
        {
            calls++;
            return false;
        }, TimeSpan.Zero);

        Assert.IsFalse(runtime.TryDelete(CreateMessage(resolver, "locked.eml", 0, 0, null)));
        Assert.AreEqual(MessageFileDeletionRuntime.MaxAttempts, calls);
    }

    [TestMethod]
    public void TryDeleteAll_DoesNotTouchFilesWhenSqlDeletionFailed()
    {
        using var fixture = new TemporaryDataDirectory();
        var resolver = new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(fixture.Path));
        var calls = 0;
        var runtime = new MessageFileDeletionRuntime(resolver, _ =>
        {
            calls++;
            return true;
        }, TimeSpan.Zero);

        var result = new ImapFolderAdministrationDeletionResult(
            Succeeded: false,
            DeletedMessages: new[] { CreateMessage(resolver, "not-touched.eml", 0, 0, null) });

        Assert.IsFalse(runtime.TryDeleteAll(result));
        Assert.AreEqual(0, calls);
    }

    private static ImapFolderAdministrationDeletedMessage CreateMessage(
        MessageFilePathResolver resolver,
        string fileName,
        int accountId,
        int folderId,
        string? accountAddress)
    {
        return new ImapFolderAdministrationDeletedMessage(fileName, accountId, folderId, accountAddress, MessageType: 2);
    }

    private sealed class TemporaryDataDirectory : IDisposable
    {
        public TemporaryDataDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hmail-delete-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
