using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerImapMessageAppendStoreTests
{
    [TestMethod]
    public void AppendSql_AllocatesUidInsertsMessageAndQueuesIndexing()
    {
        StringAssert.Contains(SqlServerImapMessageAppendStore.AllocateUidSql, "SET foldercurrentuid = foldercurrentuid + 1");
        StringAssert.Contains(SqlServerImapMessageAppendStore.AllocateUidSql, "OUTPUT INSERTED.foldercurrentuid");
        StringAssert.Contains(SqlServerImapMessageAppendStore.InsertAppendedMessageSql, "OUTPUT INSERTED.messageid");
        StringAssert.Contains(SqlServerImapMessageAppendStore.InsertAppendedMessageSql, "messageuid");
        StringAssert.Contains(SqlServerImapMessageAppendStore.QueueAppendedMessageForIndexingSql, "hm_message_search_queue");
    }

    [TestMethod]
    public void AppendStore_InjectsAccountSizeInvalidationCallbackAfterCommit()
    {
        var constructor = typeof(SqlServerImapMessageAppendStore).GetConstructor(
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            new[]
            {
                typeof(SqlServerConnectionFactory),
                typeof(MessageFilePathResolver),
                typeof(Action<int>)
            },
            modifiers: null);

        Assert.IsNotNull(constructor);

        var sourcePath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "..",
            "Server.Net10",
            "src",
            "HMailServer.Storage.SqlServer",
            "SqlServerImapMessageAppendStore.cs");
        var source = File.ReadAllText(Path.GetFullPath(sourcePath));

        var commitIndex = source.IndexOf("await transaction.CommitAsync", StringComparison.Ordinal);
        var callbackIndex = source.IndexOf(
            "_accountSizeInvalidationCallback?.Invoke(request.DestinationAccountId)",
            StringComparison.Ordinal);

        Assert.IsTrue(commitIndex >= 0);
        Assert.IsTrue(callbackIndex > commitIndex);
        StringAssert.Contains(source, "Action<int>? accountSizeInvalidationCallback = null");
    }

    [TestMethod]
    public async Task AppendAsync_WhenCanceledBeforeSql_DoesNotInvokeAccountSizeInvalidation()
    {
        var invalidatedAccountIds = new List<int>();
        var store = new SqlServerImapMessageAppendStore(
            new SqlServerConnectionFactory("Server=invalid;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            invalidatedAccountIds.Add);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await store.AppendAsync(
                new HMailServer.Core.Abstractions.ImapAppendRequest(
                    DestinationAccountId: 42,
                    DestinationFolderId: 7,
                    MailboxName: "INBOX",
                    Flags: 0,
                    InternalDateUtc: null,
                    RawMessage: Array.Empty<byte>()),
                cancellationTokenSource.Token));

        CollectionAssert.AreEqual(Array.Empty<int>(), invalidatedAccountIds);
    }
}
