using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerImapSequenceNumberResolverTests
{
    [TestMethod]
    public void SequenceSnapshotSql_ComputesMailboxSequenceByUid()
    {
        StringAssert.Contains(
            SqlServerImapSequenceNumberResolver.SequenceSnapshotSql,
            "ROW_NUMBER() OVER (ORDER BY m.messageuid ASC)");
        StringAssert.Contains(SqlServerImapSequenceNumberResolver.SequenceSnapshotSql, "m.messagetype = 2");
        StringAssert.Contains(SqlServerImapSequenceNumberResolver.SequenceSnapshotSql, "m.messageaccountid = @AccountId");
        StringAssert.Contains(SqlServerImapSequenceNumberResolver.SequenceSnapshotSql, "m.messagefolderid = @FolderId");
    }
}
