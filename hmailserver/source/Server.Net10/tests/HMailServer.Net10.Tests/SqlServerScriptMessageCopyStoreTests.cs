using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerScriptMessageCopyStoreTests
{
    [TestMethod]
    public void CopySql_RequiresSameAccountAndCreatesSearchableDeliveredMessage()
    {
        StringAssert.Contains(
            SqlServerScriptMessageCopyStore.LoadDestinationFolderSql,
            "f.folderaccountid = @SourceAccountId");
        StringAssert.Contains(
            SqlServerScriptMessageCopyStore.AllocateDestinationUidSql,
            "UPDATE hm_imapfolders WITH (UPDLOCK, ROWLOCK)");
        StringAssert.Contains(
            SqlServerScriptMessageCopyStore.InsertCopiedMessageSql,
            "@DestinationFolderId");
        StringAssert.Contains(
            SqlServerScriptMessageCopyStore.InsertCopiedMessageSql,
            "messagetype");
        StringAssert.Contains(
            SqlServerScriptMessageCopyStore.QueueCopiedMessageForIndexingSql,
            "hm_message_search_queue");
    }

    [TestMethod]
    public void CopyStore_InjectsAccountSizeInvalidationCallbackAfterCommit()
    {
        var constructor = typeof(SqlServerScriptMessageCopyStore).GetConstructor(
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
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "HMailServer.Storage.SqlServer", "SqlServerScriptMessageCopyStore.cs"));
        var commitIndex = source.IndexOf("await transaction.CommitAsync", StringComparison.Ordinal);
        var callbackIndex = source.IndexOf(
            "_accountSizeInvalidationCallback?.Invoke(request.SourceAccountId)",
            StringComparison.Ordinal);

        Assert.IsTrue(commitIndex >= 0);
        Assert.IsTrue(callbackIndex > commitIndex);
        StringAssert.Contains(source, "Action<int>? accountSizeInvalidationCallback = null");
    }

    [TestMethod]
    public async Task CopyAsync_WhenCanceledBeforeSql_DoesNotInvokeAccountSizeInvalidation()
    {
        var invalidatedAccountIds = new List<int>();
        var store = new SqlServerScriptMessageCopyStore(
            new SqlServerConnectionFactory("Server=invalid;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            invalidatedAccountIds.Add);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await store.CopyAsync(
                new HMailServer.Core.Abstractions.ScriptMessageCopyRequest(
                    SourceAccountId: 42,
                    DestinationFolderId: 7,
                    FromAddress: "sender@example.test",
                    Flags: 0,
                    CreatedUtc: DateTimeOffset.UtcNow,
                    MessageData: Array.Empty<byte>()),
                cancellationTokenSource.Token));

        CollectionAssert.AreEqual(Array.Empty<int>(), invalidatedAccountIds);
    }
}
