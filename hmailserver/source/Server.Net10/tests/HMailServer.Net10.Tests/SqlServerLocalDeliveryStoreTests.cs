using HMailServer.Core.Abstractions;
using HMailServer.Delivery;
using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerLocalDeliveryStoreTests
{
    [TestMethod]
    public void LocalDeliverySql_AllocatesInboxUidInsertsDeliveredMessageAndQueuesIndexing()
    {
        StringAssert.Contains(SqlServerLocalDeliveryStore.LoadAccountAddressSql, "FROM hm_accounts");
        StringAssert.Contains(SqlServerLocalDeliveryStore.LoadAccountAddressSql, "accountactive <> 0");
        StringAssert.Contains(SqlServerLocalDeliveryStore.AllocateInboxUidSql, "UPDATE hm_imapfolders WITH (UPDLOCK, ROWLOCK)");
        StringAssert.Contains(SqlServerLocalDeliveryStore.AllocateInboxUidSql, "folderparentid = -1");
        StringAssert.Contains(SqlServerLocalDeliveryStore.AllocateInboxUidSql, "LOWER(foldername) = 'inbox'");
        StringAssert.Contains(SqlServerLocalDeliveryStore.AllocateFolderUidSql, "folderid = @FolderId");
        StringAssert.Contains(SqlServerLocalDeliveryStore.InsertDeliveredMessageSql, "messagetype");
        StringAssert.Contains(SqlServerLocalDeliveryStore.InsertDeliveredMessageSql, "2,");
        StringAssert.Contains(SqlServerLocalDeliveryStore.InsertDeliveredMessageSql, "messageuid");
        StringAssert.Contains(SqlServerLocalDeliveryStore.QueueDeliveredMessageForIndexingSql, "hm_message_search_queue");
    }

    [TestMethod]
    public void LocalDeliveryStore_InjectsAccountSizeInvalidationCallbackAfterCommit()
    {
        var constructor = typeof(SqlServerLocalDeliveryStore).GetConstructor(
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            new[]
            {
                typeof(SqlServerConnectionFactory),
                typeof(MessageFilePathResolver),
                typeof(HMailServer.Core.Abstractions.ISmtpAccountRuleProcessor),
                typeof(HMailServer.Core.Abstractions.IImapMailboxStore),
                typeof(SqlServerSmtpQueueWriter),
                typeof(HMailServer.Core.Abstractions.IScriptMessageCopyStore),
                typeof(Action<int>)
            },
            modifiers: null);

        Assert.IsNotNull(constructor);
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "HMailServer.Storage.SqlServer", "SqlServerLocalDeliveryStore.cs"));
        var commitIndex = source.IndexOf("await transaction.CommitAsync", StringComparison.Ordinal);
        var callbackIndex = source.IndexOf(
            "_accountSizeInvalidationCallback?.Invoke(deliveryAccountId)",
            StringComparison.Ordinal);

        Assert.IsTrue(commitIndex >= 0);
        Assert.IsTrue(callbackIndex > commitIndex);
    }

    [TestMethod]
    public async Task DeliverAsync_WhenCanceledBeforeSql_DoesNotInvokeAccountSizeInvalidation()
    {
        var invalidatedAccountIds = new List<int>();
        var store = new SqlServerLocalDeliveryStore(
            new SqlServerConnectionFactory("Server=invalid;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            accountSizeInvalidationCallback: invalidatedAccountIds.Add);
        var message = new DeliveryQueuedMessage(
            new MessageIdentity(100, 0, 0, 0),
            "queue.eml",
            "sender@example.test",
            Size: 1234,
            CreatedUtc: DateTimeOffset.UtcNow,
            Flags: 0,
            CurrentRetryCount: 0,
            Recipients: [new DeliveryQueueRecipient(1, "user@example.test", "user@example.test", 42)]);
        var batch = new DeliveryTargetBatch(
            new DeliveryTarget(DeliveryTargetKind.LocalAccount, "local:42", "example.test", LocalAccountId: 42),
            message.Recipients);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await store.DeliverAsync(message, batch, cancellationTokenSource.Token));

        CollectionAssert.AreEqual(Array.Empty<int>(), invalidatedAccountIds);
    }
}
