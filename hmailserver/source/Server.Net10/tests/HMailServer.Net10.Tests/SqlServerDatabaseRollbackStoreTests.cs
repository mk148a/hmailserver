using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerDatabaseRollbackStoreTests
{
    [TestMethod]
    public void Constructor_RejectsMasterDatabase()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new SqlServerDatabaseRollbackStore(
                new SqlServerConnectionFactory(
                    "Server=localhost;Database=master;Integrated Security=True")));

        StringAssert.Contains(exception.Message, "non-master");
    }

    [TestMethod]
    public void Constructor_ExposesConfiguredDatabaseName()
    {
        var store = new SqlServerDatabaseRollbackStore(
            new SqlServerConnectionFactory(
                "Server=localhost;Initial Catalog=hmail_upgrade_disposable;Integrated Security=True"));

        Assert.AreEqual("hmail_upgrade_disposable", store.DatabaseName);
    }
}
