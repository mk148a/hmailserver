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
        var fileNameError = Assert.ThrowsExactly<COMException>(() => new Database().UtilGetFileNameByMessageID(1));

        Assert.AreEqual(EAccessDenied, databaseError.ErrorCode);
        Assert.AreEqual(EAccessDenied, fileNameError.ErrorCode);
    }

    [TestMethod]
    public void ApplicationDatabase_PreservesLegacyPerMemberAuthenticationAndUsesConfiguredRuntime()
    {
        const long largeMessageId = 0x1_0000_0001;
        DatabaseAdministrationRuntimeHost.Configure(
            new FixedDatabaseAdministrationStore(
                new DatabaseAdministrationSnapshot(
                    RequiredVersion: 5708,
                    CurrentVersion: 5707,
                    DatabaseType: (int)ComDatabaseType.MSSQL,
                    DatabaseExists: true,
                    IsConnected: true,
                    ServerName: @".\SQLExpress",
                    DatabaseName: "hmailserver")),
            new FixedMessageFileNameLookup(
                new Dictionary<long, string>
                {
                    [largeMessageId] = @"{A1}\message.eml"
                }));
        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));

        var database = application.Database;

        Assert.AreEqual(5708, database.RequiredVersion);
        Assert.AreEqual(5707, database.CurrentVersion);
        Assert.IsTrue(database.RequiresUpgrade);
        Assert.IsTrue(database.DatabaseExists);
        Assert.IsTrue(database.IsConnected);

        var typeDenied = Assert.ThrowsExactly<COMException>(() => _ = database.DatabaseType);
        var operationDenied = Assert.ThrowsExactly<COMException>(() => database.ExecuteSQL("select 1"));
        var fileNameDenied = Assert.ThrowsExactly<COMException>(() => database.UtilGetFileNameByMessageID(largeMessageId));
        Assert.AreEqual(EAccessDenied, typeDenied.ErrorCode);
        Assert.AreEqual(EAccessDenied, operationDenied.ErrorCode);
        Assert.AreEqual(EAccessDenied, fileNameDenied.ErrorCode);

        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));

        Assert.AreEqual(ComDatabaseType.MSSQL, database.DatabaseType);
        Assert.AreEqual(@".\SQLExpress", database.ServerName);
        Assert.AreEqual("hmailserver", database.DatabaseName);
        Assert.AreEqual(@"{A1}\message.eml", database.UtilGetFileNameByMessageID(largeMessageId));
        Assert.AreEqual(string.Empty, database.UtilGetFileNameByMessageID(999));
        AssertOperationPending(() => database.ExecuteSQL("select 1"));
        AssertOperationPending(() => database.ExecuteSQLWithReturn("select 1"));
        AssertOperationPending(database.BeginTransaction);
        AssertNoTransactionStarted(database.CommitTransaction);
        AssertNoTransactionStarted(database.RollbackTransaction);
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
    public void AuthenticatedTransactionMethodsUseConfiguredMutationStoreAndPreserveBoundaries()
    {
        var store = new RecordingMutationStore();
        var authenticated = false;
        IInterfaceDatabase database = Database.CreateForApplication(
            new DatabaseAdministrationSnapshot(
                RequiredVersion: 5708,
                CurrentVersion: 5708,
                DatabaseType: (int)ComDatabaseType.MSSQL,
                DatabaseExists: true,
                IsConnected: true,
                ServerName: "isolated",
                DatabaseName: "disposable"),
            () => authenticated,
            store: store);

        var denied = Assert.ThrowsExactly<COMException>(database.BeginTransaction);
        Assert.AreEqual(EAccessDenied, denied.ErrorCode);

        authenticated = true;

        database.BeginTransaction();
        Assert.IsTrue(store.Transaction.Began);
        var scriptPath = Path.Combine(Path.GetTempPath(), $"hmailserver-net10-{Guid.NewGuid():N}.sql");
        File.WriteAllText(scriptPath, "select 1\r\n\r\nselect 2");
        try
        {
            database.ExecuteSQLScript(scriptPath);
            CollectionAssert.AreEqual(
                new[] { "select 1", "select 2" },
                store.Transaction.Scripts.ToArray());
        }
        finally
        {
            File.Delete(scriptPath);
        }
        database.CommitTransaction();
        Assert.IsTrue(store.Transaction.Committed);
        Assert.IsTrue(store.Transaction.Disposed);

        database.BeginTransaction();
        database.RollbackTransaction();
        Assert.IsTrue(store.Transaction.RolledBack);

        var noTransaction = Assert.ThrowsExactly<COMException>(database.CommitTransaction);
        Assert.AreEqual(EFail, noTransaction.ErrorCode);
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

    private static void AssertNoTransactionStarted(Action action)
    {
        var error = Assert.ThrowsExactly<COMException>(action);

        Assert.AreEqual(EFail, error.ErrorCode);
        StringAssert.Contains(error.Message, "No transaction started");
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

    private sealed class RecordingMutationStore : IDatabaseAdministrationMutationStore
    {
        public RecordingMutationStore()
        {
            Transaction = new RecordingTransaction();
        }

        public RecordingTransaction Transaction { get; }

        public ValueTask<DatabaseAdministrationSnapshot> GetDatabaseAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(
                new DatabaseAdministrationSnapshot(
                    RequiredVersion: 5708,
                    CurrentVersion: 5708,
                    DatabaseType: (int)ComDatabaseType.MSSQL,
                    DatabaseExists: true,
                    IsConnected: true,
                    ServerName: "isolated",
                    DatabaseName: "disposable"));

        public ValueTask<IDatabaseAdministrationTransaction> BeginTransactionAsync(
            CancellationToken cancellationToken)
        {
            Transaction.Began = true;
            return ValueTask.FromResult<IDatabaseAdministrationTransaction>(Transaction);
        }
    }

    private sealed class RecordingTransaction : IDatabaseAdministrationTransaction
    {
        public bool Began { get; set; }
        public bool Committed { get; private set; }
        public bool RolledBack { get; private set; }
        public bool Disposed { get; private set; }
        public List<string> Scripts { get; } = [];

        public ValueTask ExecuteScriptAsync(string filename, CancellationToken cancellationToken)
        {
            Scripts.AddRange(
                File.ReadAllText(filename)
                    .Replace("\r\n", "\n", StringComparison.Ordinal)
                    .Split("\n\n", StringSplitOptions.None)
                    .Select(static command => command.TrimStart('\n', ' ', '\t'))
                    .Where(static command => command.Length > 0));
            return ValueTask.CompletedTask;
        }

        public ValueTask CommitAsync(CancellationToken cancellationToken)
        {
            Committed = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask RollbackAsync(CancellationToken cancellationToken)
        {
            RolledBack = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FixedMessageFileNameLookup(IReadOnlyDictionary<long, string> fileNames)
        : IMessageFileNameLookup
    {
        public ValueTask<string> GetFileNameByMessageIdAsync(
            long messageId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(
                fileNames.TryGetValue(messageId, out var fileName)
                    ? fileName
                    : string.Empty);

        public ValueTask<long?> GetMessageIdByFileNameAsync(
            string fileName,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<long?>(null);
    }
}
