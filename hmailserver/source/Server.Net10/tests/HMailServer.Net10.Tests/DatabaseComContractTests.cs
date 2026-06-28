using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class DatabaseComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    [TestMethod]
    public void Interface_PreservesLegacyIidDispatchIdsAndCompleteVtableOrder()
    {
        Assert.AreEqual(
            new Guid("F58B6982-4C39-11D9-B629-F87B01E1264F"),
            typeof(ComDatabaseType).GUID);
        CollectionAssert.AreEqual(
            new[] { 0, 1, 2, 3, 4 },
            Enum.GetValues<ComDatabaseType>().Select(static value => (int)value).ToArray());

        Assert.AreEqual(
            new Guid("90471F47-FE77-46C7-ADDB-F800B7ED0F66"),
            typeof(IInterfaceDatabase).GUID);
        Assert.AreEqual(
            ComInterfaceType.InterfaceIsDual,
            typeof(IInterfaceDatabase).GetCustomAttribute<InterfaceTypeAttribute>()?.Value);
        Assert.AreEqual(
            TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable,
            typeof(IInterfaceDatabase).GetCustomAttribute<TypeLibTypeAttribute>()?.Value);

        CollectionAssert.AreEqual(
            new[]
            {
                "get_RequiredVersion",
                "get_CurrentVersion",
                "ExecuteSQL",
                "get_DatabaseType",
                "UtilGetFileNameByMessageID",
                "get_RequiresUpgrade",
                "CreateInternalDatabase",
                "CreateExternalDatabase",
                "get_DatabaseExists",
                "BeginTransaction",
                "CommitTransaction",
                "RollbackTransaction",
                "ExecuteSQLScript",
                "SetDefaultDatabase",
                "get_IsConnected",
                "get_ServerName",
                "get_DatabaseName",
                "ExecuteSQLWithReturn",
                "EnsurePrerequisites"
            },
            typeof(IInterfaceDatabase)
                .GetMethods()
                .OrderBy(static method => method.MetadataToken)
                .Select(static method => method.Name)
                .ToArray());

        Assert.AreEqual(
            1,
            typeof(IInterfaceDatabase).GetProperty(nameof(IInterfaceDatabase.RequiredVersion))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            19,
            typeof(IInterfaceDatabase).GetMethod(nameof(IInterfaceDatabase.EnsurePrerequisites))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClass_PreservesLegacyIdentityAndDefaultInterface()
    {
        var type = typeof(Database);

        Assert.AreEqual(new Guid("2F5BEF2E-C713-4826-88AE-A5FD9921907B"), type.GUID);
        Assert.AreEqual("hMailServer.Database.1", type.GetCustomAttribute<ProgIdAttribute>()?.Value);
        Assert.AreEqual(ClassInterfaceType.None, type.GetCustomAttribute<ClassInterfaceAttribute>()?.Value);
        Assert.AreEqual(typeof(IInterfaceDatabase), type.GetCustomAttribute<ComDefaultInterfaceAttribute>()?.Value);
        Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var databaseError = Assert.ThrowsExactly<COMException>(() => _ = new Database().RequiredVersion);

        Assert.AreEqual(EAccessDenied, databaseError.ErrorCode);
    }

    [TestMethod]
    public void ApplicationDatabase_PreservesLegacyPerMemberAuthenticationAndUsesConfiguredRuntime()
    {
        DatabaseAdministrationRuntimeHost.Configure(
            new FixedDatabaseAdministrationStore(
                new DatabaseAdministrationSnapshot(
                    RequiredVersion: 5708,
                    CurrentVersion: 5707,
                    DatabaseType: (int)ComDatabaseType.MSSQL,
                    DatabaseExists: true,
                    IsConnected: true,
                    ServerName: @".\SQLExpress",
                    DatabaseName: "hmailserver")));
        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));

        var database = application.Database;

        Assert.AreEqual(5708, database.RequiredVersion);
        Assert.AreEqual(5707, database.CurrentVersion);
        Assert.IsTrue(database.RequiresUpgrade);
        Assert.IsTrue(database.DatabaseExists);
        Assert.IsTrue(database.IsConnected);

        var typeDenied = Assert.ThrowsExactly<COMException>(() => _ = database.DatabaseType);
        var operationDenied = Assert.ThrowsExactly<COMException>(() => database.ExecuteSQL("select 1"));
        Assert.AreEqual(EAccessDenied, typeDenied.ErrorCode);
        Assert.AreEqual(EAccessDenied, operationDenied.ErrorCode);

        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));

        Assert.AreEqual(ComDatabaseType.MSSQL, database.DatabaseType);
        Assert.AreEqual(@".\SQLExpress", database.ServerName);
        Assert.AreEqual("hmailserver", database.DatabaseName);
        AssertOperationPending(() => database.ExecuteSQL("select 1"));
        AssertOperationPending(() => database.ExecuteSQLWithReturn("select 1"));
        AssertOperationPending(() => database.UtilGetFileNameByMessageID(1));
        AssertOperationPending(database.BeginTransaction);
        AssertOperationPending(database.CommitTransaction);
        AssertOperationPending(database.RollbackTransaction);
        AssertOperationPending(() => database.ExecuteSQLScript("upgrade.sql"));
        AssertOperationPending(database.CreateInternalDatabase);
        AssertOperationPending(
            () => database.CreateExternalDatabase(
                ComDatabaseType.MSSQL,
                @".\SQLExpress",
                1433,
                "hmailserver",
                "sa",
                "secret"));
        AssertOperationPending(
            () => database.SetDefaultDatabase(
                ComDatabaseType.MSSQL,
                @".\SQLExpress",
                1433,
                "hmailserver",
                "sa",
                "secret"));
        AssertOperationPending(() => database.EnsurePrerequisites(5708));
    }

    [TestMethod]
    public void DisconnectedSnapshot_ExposesConnectionStateAndKeepsVersionDependentMembersUnavailable()
    {
        IInterfaceDatabase database = Database.CreateForApplication(
            new DatabaseAdministrationSnapshot(
                RequiredVersion: 5708,
                CurrentVersion: null,
                DatabaseType: (int)ComDatabaseType.Unknown,
                DatabaseExists: false,
                IsConnected: false,
                ServerName: string.Empty,
                DatabaseName: string.Empty),
            static () => true);

        Assert.AreEqual(5708, database.RequiredVersion);
        Assert.IsFalse(database.DatabaseExists);
        Assert.IsFalse(database.IsConnected);

        var currentVersionError = Assert.ThrowsExactly<COMException>(() => _ = database.CurrentVersion);
        var requiresUpgradeError = Assert.ThrowsExactly<COMException>(() => _ = database.RequiresUpgrade);

        Assert.AreEqual(EFail, currentVersionError.ErrorCode);
        Assert.AreEqual(EFail, requiresUpgradeError.ErrorCode);
    }

    private static void AssertOperationPending(Action action)
    {
        var error = Assert.ThrowsExactly<COMException>(action);

        Assert.AreEqual(ENotImplemented, error.ErrorCode);
    }

    private sealed class RecordingAdministratorAuthenticationProvider(string password)
        : IServerAdministratorAuthenticationProvider
    {
        public bool Authenticate(string username, string attemptedPassword) =>
            username.Equals("Administrator", StringComparison.OrdinalIgnoreCase)
            && attemptedPassword == password;
    }

    private sealed class FixedDatabaseAdministrationStore(DatabaseAdministrationSnapshot snapshot)
        : IDatabaseAdministrationStore
    {
        public ValueTask<DatabaseAdministrationSnapshot> GetDatabaseAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(snapshot);
    }
}
