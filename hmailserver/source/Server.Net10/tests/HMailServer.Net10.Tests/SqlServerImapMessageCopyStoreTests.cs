using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerImapMessageCopyStoreTests
{
    [TestMethod]
    public void PlanCopy_UsesUidOrSequenceRanges()
    {
        var plan = SqlServerImapMessageCopyStore.PlanCopy(
            new ImapCopyRequest(
                SourceAccountId: 77,
                SourceFolderId: 88,
                DestinationAccountId: 77,
                DestinationFolderId: 99,
                MessageSet: [new ImapIdRange(101, null)],
                UseUid: true,
                DeleteSource: false));

        StringAssert.Contains(plan.CommandText, "ROW_NUMBER() OVER (ORDER BY m.messageuid ASC)");
        StringAssert.Contains(plan.CommandText, "messageuid >= @RangeStart0");
        Assert.AreEqual(77, plan.Parameters["@SourceAccountId"]);
        Assert.AreEqual(88, plan.Parameters["@SourceFolderId"]);
        Assert.AreEqual(101L, plan.Parameters["@RangeStart0"]);

        var sequencePlan = SqlServerImapMessageCopyStore.PlanCopy(
            new ImapCopyRequest(
                SourceAccountId: 77,
                SourceFolderId: 88,
                DestinationAccountId: 77,
                DestinationFolderId: 99,
                MessageSet: [new ImapIdRange(1, 10)],
                UseUid: false,
                DeleteSource: true));

        StringAssert.Contains(sequencePlan.CommandText, "sequencenumber BETWEEN @RangeStart0 AND @RangeEnd0");
    }

    [TestMethod]
    public void CopySql_AllocatesDestinationUidAndQueuesIndexing()
    {
        StringAssert.Contains(SqlServerImapMessageCopyStore.AllocateUidSql, "SET foldercurrentuid = foldercurrentuid + 1");
        StringAssert.Contains(SqlServerImapMessageCopyStore.AllocateUidSql, "OUTPUT INSERTED.foldercurrentuid");
        StringAssert.Contains(SqlServerImapMessageCopyStore.InsertCopiedMessageSql, "OUTPUT INSERTED.messageid");
        StringAssert.Contains(SqlServerImapMessageCopyStore.InsertCopiedMessageSql, "messageuid");
        StringAssert.Contains(SqlServerImapMessageCopyStore.QueueCopiedMessageForIndexingSql, "hm_message_search_queue");
        StringAssert.Contains(SqlServerImapMessageCopyStore.DeleteSourceMessageSql, "DELETE FROM hm_messages");
    }

    [TestMethod]
    public void CopyStore_InjectsPostCommitInvalidationForDestinationAndDistinctSource()
    {
        var constructor = typeof(SqlServerImapMessageCopyStore).GetConstructor(
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
            "SqlServerImapMessageCopyStore.cs");
        var source = File.ReadAllText(Path.GetFullPath(sourcePath));
        var commitIndex = source.IndexOf("await transaction.CommitAsync", StringComparison.Ordinal);
        var insertIndex = source.IndexOf("copied = await InsertCopiesAsync", StringComparison.Ordinal);
        var callbackIndex = source.IndexOf("InvalidateAccountSizesAfterCommit(request)", StringComparison.Ordinal);

        Assert.IsTrue(commitIndex >= 0);
        Assert.IsTrue(insertIndex >= 0);
        Assert.IsTrue(callbackIndex > insertIndex);
        StringAssert.Contains(source, "request.DeleteSource && request.SourceAccountId != request.DestinationAccountId");
        StringAssert.Contains(source, "Action<int>? accountSizeInvalidationCallback = null");
    }

    [TestMethod]
    public async Task CopyAsync_WhenCanceledBeforeSql_DoesNotInvokeAccountSizeInvalidation()
    {
        var invalidatedAccountIds = new List<int>();
        var store = new SqlServerImapMessageCopyStore(
            new SqlServerConnectionFactory("Server=invalid;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            invalidatedAccountIds.Add);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var enumerator = store.CopyAsync(
                new ImapCopyRequest(
                    SourceAccountId: 11,
                    SourceFolderId: 12,
                    DestinationAccountId: 42,
                    DestinationFolderId: 13,
                    MessageSet: [new ImapIdRange(1, 1)],
                    UseUid: true,
                    DeleteSource: true),
                cancellationTokenSource.Token)
            .GetAsyncEnumerator();
        try
        {
            await Assert.ThrowsAsync<OperationCanceledException>(() => enumerator.MoveNextAsync().AsTask());
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        CollectionAssert.AreEqual(Array.Empty<int>(), invalidatedAccountIds);
    }
}
