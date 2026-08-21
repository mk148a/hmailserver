using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("90471F47-FE77-46C7-ADDB-F800B7ED0F66")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceDatabase
{
    [DispId(1)]
    int RequiredVersion { get; }

    [DispId(2)]
    int CurrentVersion { get; }

    [DispId(3)]
    void ExecuteSQL([MarshalAs(UnmanagedType.BStr)] string sqlStatement);

    [DispId(4)]
    ComDatabaseType DatabaseType { get; }

    [DispId(5)]
    [return: MarshalAs(UnmanagedType.BStr)]
    string UtilGetFileNameByMessageID(long messageId);

    [DispId(6)]
    bool RequiresUpgrade { [return: MarshalAs(UnmanagedType.VariantBool)] get; }

    [DispId(7)]
    void CreateInternalDatabase();

    [DispId(8)]
    void CreateExternalDatabase(
        ComDatabaseType serverType,
        [MarshalAs(UnmanagedType.BStr)] string serverName,
        int port,
        [MarshalAs(UnmanagedType.BStr)] string databaseName,
        [MarshalAs(UnmanagedType.BStr)] string username,
        [MarshalAs(UnmanagedType.BStr)] string password);

    [DispId(9)]
    bool DatabaseExists { [return: MarshalAs(UnmanagedType.VariantBool)] get; }

    [DispId(10)]
    void BeginTransaction();

    [DispId(11)]
    void CommitTransaction();

    [DispId(12)]
    void RollbackTransaction();

    [DispId(13)]
    void ExecuteSQLScript([MarshalAs(UnmanagedType.BStr)] string filename);

    [DispId(14)]
    void SetDefaultDatabase(
        ComDatabaseType serverType,
        [MarshalAs(UnmanagedType.BStr)] string serverName,
        int port,
        [MarshalAs(UnmanagedType.BStr)] string databaseName,
        [MarshalAs(UnmanagedType.BStr)] string username,
        [MarshalAs(UnmanagedType.BStr)] string password);

    [DispId(15)]
    bool IsConnected { [return: MarshalAs(UnmanagedType.VariantBool)] get; }

    [DispId(16)]
    string ServerName { [return: MarshalAs(UnmanagedType.BStr)] get; }

    [DispId(17)]
    string DatabaseName { [return: MarshalAs(UnmanagedType.BStr)] get; }

    [DispId(18)]
    int ExecuteSQLWithReturn([MarshalAs(UnmanagedType.BStr)] string sqlStatement);

    [DispId(19)]
    void EnsurePrerequisites(int databaseVersion);
}

[ComVisible(true)]
[Guid("2F5BEF2E-C713-4826-88AE-A5FD9921907B")]
[ProgId("hMailServer.Database.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceDatabase))]
public sealed class Database : IInterfaceDatabase
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly DatabaseAdministrationSnapshot? _snapshot;
    private readonly Func<bool>? _isServerAdministrator;
    private readonly IMessageFileNameLookup? _messageFileNameLookup;
    private readonly IDatabaseAdministrationMutationStore? _mutationStore;
    private IDatabaseAdministrationTransaction? _transaction;

    public Database()
    {
    }

    private Database(
        DatabaseAdministrationSnapshot snapshot,
        Func<bool> isServerAdministrator,
        IMessageFileNameLookup? messageFileNameLookup,
        IDatabaseAdministrationStore? store)
    {
        _snapshot = snapshot;
        _isServerAdministrator = isServerAdministrator;
        _messageFileNameLookup = messageFileNameLookup;
        _mutationStore = store as IDatabaseAdministrationMutationStore;
    }

    public int RequiredVersion => Snapshot.RequiredVersion;

    public int CurrentVersion => CurrentVersionOrThrow();

    public ComDatabaseType DatabaseType
    {
        get
        {
            EnsureServerAdministrator();
            return (ComDatabaseType)Snapshot.DatabaseType;
        }
    }

    public bool RequiresUpgrade => CurrentVersionOrThrow() < Snapshot.RequiredVersion;

    public bool DatabaseExists => Snapshot.DatabaseExists;

    public bool IsConnected => Snapshot.IsConnected;

    public string ServerName
    {
        get
        {
            EnsureServerAdministrator();
            return Snapshot.ServerName;
        }
    }

    public string DatabaseName
    {
        get
        {
            EnsureServerAdministrator();
            return Snapshot.DatabaseName;
        }
    }

    internal static Database CreateForApplication(
        DatabaseAdministrationSnapshot snapshot,
        Func<bool> isServerAdministrator,
        IMessageFileNameLookup? messageFileNameLookup = null,
        IDatabaseAdministrationStore? store = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(isServerAdministrator);
        return new Database(snapshot, isServerAdministrator, messageFileNameLookup, store);
    }

    public void ExecuteSQL(string sqlStatement) => Unavailable();

    public string UtilGetFileNameByMessageID(long messageId)
    {
        EnsureServerAdministrator();
        if (_messageFileNameLookup is null)
        {
            return Unavailable<string>();
        }

        return _messageFileNameLookup
            .GetFileNameByMessageIdAsync(messageId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }

    public void CreateInternalDatabase() => Unavailable();

    public void CreateExternalDatabase(
        ComDatabaseType serverType,
        string serverName,
        int port,
        string databaseName,
        string username,
        string password) =>
        Unavailable();

    public void BeginTransaction()
    {
        EnsureServerAdministrator();
        if (_mutationStore is null)
        {
            Unavailable();
            return;
        }

        if (_transaction is not null)
        {
            throw new COMException("A database transaction is already active.", EFail);
        }

        try
        {
            _transaction = _mutationStore.BeginTransactionAsync(CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new COMException(exception.Message, EFail);
        }
    }

    public void CommitTransaction() => CompleteTransaction(commit: true);

    public void RollbackTransaction() => CompleteTransaction(commit: false);

    public void ExecuteSQLScript(string filename)
    {
        EnsureServerAdministrator();
        if (_mutationStore is null)
        {
            Unavailable();
            return;
        }

        var transaction = _transaction
            ?? throw new COMException("No transaction started.", EFail);
        try
        {
            transaction.ExecuteScriptAsync(filename, CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new COMException(exception.Message, EFail);
        }
    }

    public void SetDefaultDatabase(
        ComDatabaseType serverType,
        string serverName,
        int port,
        string databaseName,
        string username,
        string password) =>
        Unavailable();

    public int ExecuteSQLWithReturn(string sqlStatement) => Unavailable<int>();

    public void EnsurePrerequisites(int databaseVersion) => Unavailable();

    private DatabaseAdministrationSnapshot Snapshot =>
        _snapshot ?? throw new COMException(
            "Database access requires a process-hosted hMailServer application object.",
            EAccessDenied);

    private int CurrentVersionOrThrow() =>
        Snapshot.CurrentVersion
        ?? throw new COMException(
            "The connection to the database is not available. Please check the hMailServer error log for details.",
            EFail);

    private void EnsureServerAdministrator()
    {
        _ = Snapshot;

        if (_isServerAdministrator is null || !_isServerAdministrator())
        {
            throw new COMException(
                "You do not have access to this property / method. Ensure that hMailServer.Application.Authenticate() is called with proper login credentials.",
                EAccessDenied);
        }
    }

    private void CompleteTransaction(bool commit)
    {
        EnsureServerAdministrator();
        var transaction = _transaction;
        if (transaction is null)
        {
            throw new COMException("No transaction started.", EFail);
        }

        _transaction = null;
        try
        {
            if (commit)
            {
                transaction.CommitAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
            }
            else
            {
                transaction.RollbackAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
            }
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new COMException(exception.Message, EFail);
        }
        finally
        {
            transaction.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private T Unavailable<T>()
    {
        Unavailable();
        return default!;
    }

    private void Unavailable()
    {
        EnsureServerAdministrator();
        throw new COMException(
            "This Database member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(false)]
public static class DatabaseAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static IDatabaseAdministrationStore? _store;
    private static IMessageFileNameLookup? _messageFileNameLookup;

    public static void Configure(
        IDatabaseAdministrationStore store,
        IMessageFileNameLookup? messageFileNameLookup = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
        Volatile.Write(ref _messageFileNameLookup, messageFileNameLookup);
    }

    internal static Database CreateApplicationAdapter(Func<bool> isServerAdministrator)
    {
        ArgumentNullException.ThrowIfNull(isServerAdministrator);

        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer database administration runtime has not been initialized.",
                CoENotInitialized);

        var snapshot = store
            .GetDatabaseAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return Database.CreateForApplication(
            snapshot,
            isServerAdministrator,
            Volatile.Read(ref _messageFileNameLookup),
            store);
    }
}
