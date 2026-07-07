using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerImportMessageFromFileStoreTests
{
    [TestMethod]
    public void LookupAndPathUpdateSql_AreSingleTableAndParameterBound()
    {
        var lookup = SqlServerImportMessageFromFileStore.FindMessageSql;
        StringAssert.Contains(lookup, "SELECT TOP (1) messageid");
        StringAssert.Contains(lookup, "FROM hm_messages");
        StringAssert.Contains(lookup, "messagefilename = @FileName");
        Assert.IsFalse(lookup.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        AssertNoMutation(lookup);

        var update = SqlServerImportMessageFromFileStore.UpdateMessageFileNameSql;
        StringAssert.Contains(update, "UPDATE hm_messages");
        StringAssert.Contains(update, "messagefilename = @FileName");
        StringAssert.Contains(update, "messageid = @MessageId");
        Assert.IsFalse(update.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(update.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void DeliveredSql_ResolvesFolderAndAllocatesUidWithoutNewFolderMutation()
    {
        var findFolder = SqlServerImportMessageFromFileStore.FindFolderSql;
        StringAssert.Contains(findFolder, "SELECT TOP (1) folderid");
        StringAssert.Contains(findFolder, "FROM hm_imapfolders");
        StringAssert.Contains(findFolder, "folderaccountid = @AccountId");
        StringAssert.Contains(findFolder, "folderparentid = @ParentFolderId");
        StringAssert.Contains(findFolder, "LOWER(foldername) = LOWER(@FolderName)");
        AssertNoMutation(findFolder);

        var allocation = SqlServerImportMessageFromFileStore.AllocateFolderUidSql;
        StringAssert.Contains(allocation, "UPDATE hm_imapfolders WITH (UPDLOCK, ROWLOCK)");
        StringAssert.Contains(allocation, "foldercurrentuid = foldercurrentuid + 1");
        StringAssert.Contains(allocation, "folderaccountid = @AccountId");
        StringAssert.Contains(allocation, "folderid = @FolderId");
        Assert.IsFalse(allocation.Contains("INSERT INTO hm_imapfolders", StringComparison.OrdinalIgnoreCase));

        var insert = SqlServerImportMessageFromFileStore.InsertDeliveredMessageSql;
        StringAssert.Contains(insert, "INSERT INTO hm_messages");
        StringAssert.Contains(insert, "@AccountId");
        StringAssert.Contains(insert, "@FolderId");
        StringAssert.Contains(insert, "@MessageUid");
        StringAssert.Contains(insert, "@FileName");
        StringAssert.Contains(insert, "@MessageFrom");
        StringAssert.Contains(insert, "@MessageSize");
        StringAssert.Contains(insert, "@MessageCreateTime");
        StringAssert.Contains(insert, "    2,");
        StringAssert.Contains(insert, "    32,");
        StringAssert.Contains(insert, "    0,");
        AssertNoSecretOrContentAccess(insert);
    }

    [TestMethod]
    public void QueueSql_UsesLockedInsertRecipientsThenBoundedUnlock()
    {
        var insert = SqlServerImportMessageFromFileStore.InsertQueuedMessageSql;
        StringAssert.Contains(insert, "INSERT INTO hm_messages");
        StringAssert.Contains(insert, "    1,");
        StringAssert.Contains(insert, "@FileName");
        StringAssert.Contains(insert, "@MessageFrom");
        AssertNoSecretOrContentAccess(insert);

        var recipient = SqlServerImportMessageFromFileStore.InsertRecipientSql;
        StringAssert.Contains(recipient, "INSERT INTO hm_messagerecipients");
        StringAssert.Contains(recipient, "@RecipientAddress");
        StringAssert.Contains(recipient, "@LocalAccountId");
        StringAssert.Contains(recipient, "@OriginalAddress");

        var unlock = SqlServerImportMessageFromFileStore.UnlockQueuedMessageSql;
        StringAssert.Contains(unlock, "UPDATE hm_messages");
        StringAssert.Contains(unlock, "messagelocked = 0");
        StringAssert.Contains(unlock, "messageid = @MessageId");
        StringAssert.Contains(unlock, "messagetype = 1");
    }

    private static void AssertNoMutation(string sql)
    {
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertNoSecretOrContentAccess(string sql)
    {
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("password", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("messagebody", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("messagecontent", StringComparison.OrdinalIgnoreCase));
    }
}
