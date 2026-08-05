using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("D6B91C3A-90C1-4943-B818-EE66119E4702")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceSURBLServers
{
    [DispId(0)]
    IInterfaceSURBLServer this[int index] { get; }

    [DispId(1)]
    int Count { get; }

    [DispId(2)]
    void DeleteByDBID(int databaseId);

    [DispId(3)]
    IInterfaceSURBLServer Add();

    [DispId(5)]
    [SpecialName]
    IInterfaceSURBLServer get_ItemByDBID(int databaseId);

    [DispId(6)]
    void Refresh();

    [DispId(7)]
    [SpecialName]
    IInterfaceSURBLServer get_ItemByDNSHost([MarshalAs(UnmanagedType.BStr)] string dnsHost);
}

[ComVisible(true)]
[Guid("A4866EDD-F0B8-49C7-A477-57D469F7D7D4")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceSURBLServer
{
    [DispId(1)]
    bool Active
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(2)]
    int ID { get; }

    [DispId(3)]
    string DNSHost
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(4)]
    string RejectMessage
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(6)]
    void Save();

    [DispId(7)]
    int Score { get; set; }

    [DispId(8)]
    void Delete();
}

[ComVisible(true)]
[Guid("FCD94E5F-F05F-400B-8345-AFC7FDD6626E")]
[ProgId("hMailServer.SURBLServers.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceSURBLServers))]
public sealed class SURBLServers : IInterfaceSURBLServers
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private SurblServerAdministrationSnapshot[]? _servers;
    private readonly Func<IReadOnlyList<SurblServerAdministrationSnapshot>>? _reload;
    private readonly Func<SurblServerAdministrationSnapshot, int>? _insert;
    private readonly Func<SurblServerAdministrationSnapshot, bool>? _update;
    private readonly Func<int, bool>? _delete;
    private readonly Action<SurblServerAdministrationSnapshot>? _append;
    private readonly Action<SurblServerAdministrationSnapshot>? _replace;
    private readonly Func<bool>? _isServerAdministrator;

    public SURBLServers()
    {
    }

    private SURBLServers(
        IReadOnlyList<SurblServerAdministrationSnapshot> servers,
        Func<IReadOnlyList<SurblServerAdministrationSnapshot>>? reload,
        Func<SurblServerAdministrationSnapshot, int>? insert,
        Func<SurblServerAdministrationSnapshot, bool>? update,
        Func<int, bool>? delete,
        Func<bool>? isServerAdministrator)
    {
        _servers = servers.ToArray();
        _reload = reload;
        _insert = insert;
        _update = update;
        _delete = delete;
        _isServerAdministrator = isServerAdministrator;
        _append = Append;
        _replace = Replace;
    }

    public int Count => GetServers().Count;

    public IInterfaceSURBLServer this[int index]
    {
        get
        {
            var servers = GetServers();
            if (index < 0 || index >= servers.Count)
            {
                throw new COMException("SURBL server index was outside the collection.", DispEBadIndex);
            }

            return SURBLServer.CreateAuthorized(
                servers[index],
                update: _update,
                delete: _delete is null ? null : DeleteByDBID,
                replace: _replace,
                isServerAdministrator: _isServerAdministrator);
        }
    }

    public void DeleteByDBID(int databaseId)
    {
        var servers = GetServers();
        EnsureServerAdministrator();
        if (_delete is null)
        {
            Unavailable();
            return;
        }

        if (!servers.Any(server => server.Id == databaseId))
        {
            return;
        }

        try
        {
            if (!_delete(databaseId))
            {
                throw new InvalidOperationException(
                    "The SURBL server delete did not affect the selected database row.");
            }

            Volatile.Write(
                ref _servers,
                servers.Where(server => server.Id != databaseId).ToArray());
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to delete the SURBL server from the database.",
                EFail);
        }
    }

    public IInterfaceSURBLServer Add()
    {
        _ = GetServers();
        EnsureServerAdministrator();
        if (_insert is null)
        {
            return Unavailable<IInterfaceSURBLServer>();
        }

        return SURBLServer.CreateAuthorized(
            new SurblServerAdministrationSnapshot(0, false, string.Empty, string.Empty, 0),
            insert: _insert,
            delete: _delete is null ? null : DeleteByDBID,
            append: _append,
            isServerAdministrator: _isServerAdministrator);
    }

    public IInterfaceSURBLServer get_ItemByDBID(int databaseId)
    {
        var match = GetServers().FirstOrDefault(server => server.Id == databaseId);

        return match is null
            ? throw new COMException("No SURBL server with the specified database identifier exists.", DispEBadIndex)
            : SURBLServer.CreateAuthorized(
                match,
                update: _update,
                delete: _delete is null ? null : DeleteByDBID,
                replace: _replace,
                isServerAdministrator: _isServerAdministrator);
    }

    public void Refresh()
    {
        _ = GetServers();
        if (_reload is null)
        {
            Unavailable();
            return;
        }

        try
        {
            var servers = _reload();
            ArgumentNullException.ThrowIfNull(servers);
            Volatile.Write(ref _servers, servers.ToArray());
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to retrieve a list of SURBL servers from the database.",
                EFail);
        }
    }

    public IInterfaceSURBLServer get_ItemByDNSHost(string dnsHost)
    {
        var match = GetServers().FirstOrDefault(
            server => string.Equals(server.DnsHost, dnsHost, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? null!
            : SURBLServer.CreateAuthorized(
                match,
                update: _update,
                delete: _delete is null ? null : DeleteByDBID,
                replace: _replace,
                isServerAdministrator: _isServerAdministrator);
    }

    internal static SURBLServers CreateAuthorized(
        IReadOnlyList<SurblServerAdministrationSnapshot> servers,
        Func<IReadOnlyList<SurblServerAdministrationSnapshot>>? reload = null,
        Func<SurblServerAdministrationSnapshot, int>? insert = null,
        Func<SurblServerAdministrationSnapshot, bool>? update = null,
        Func<int, bool>? delete = null,
        Func<bool>? isServerAdministrator = null)
    {
        ArgumentNullException.ThrowIfNull(servers);
        return new SURBLServers(servers, reload, insert, update, delete, isServerAdministrator);
    }

    private IReadOnlyList<SurblServerAdministrationSnapshot> GetServers()
    {
        return Volatile.Read(ref _servers)
            ?? throw new COMException(
                "SURBLServers access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private void Append(SurblServerAdministrationSnapshot server)
    {
        var servers = GetServers();
        Volatile.Write(ref _servers, servers.Append(server).ToArray());
    }

    private void Replace(SurblServerAdministrationSnapshot server)
    {
        var servers = GetServers();
        Volatile.Write(
            ref _servers,
            servers.Select(existing => existing.Id == server.Id ? server : existing).ToArray());
    }

    private void EnsureServerAdministrator()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "SURBLServers access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private void Unavailable()
    {
        _ = GetServers();
        throw new COMException(
            "This SURBLServers member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private T Unavailable<T>()
    {
        Unavailable();
        return default!;
    }
}

[ComVisible(true)]
[Guid("D875AEC4-7AA0-4C93-9F8F-141324C80D17")]
[ProgId("hMailServer.SURBLServer.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceSURBLServer))]
public sealed class SURBLServer : IInterfaceSURBLServer
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private SurblServerAdministrationSnapshot? _server;
    private readonly Func<SurblServerAdministrationSnapshot, int>? _insert;
    private readonly Func<SurblServerAdministrationSnapshot, bool>? _update;
    private readonly Action<int>? _delete;
    private readonly Action<SurblServerAdministrationSnapshot>? _append;
    private readonly Action<SurblServerAdministrationSnapshot>? _replace;
    private readonly Func<bool>? _isServerAdministrator;

    public SURBLServer()
    {
    }

    private SURBLServer(SurblServerAdministrationSnapshot server)
    {
        _server = server;
    }

    private SURBLServer(
        SurblServerAdministrationSnapshot server,
        Func<SurblServerAdministrationSnapshot, int>? insert,
        Func<SurblServerAdministrationSnapshot, bool>? update,
        Action<int>? delete,
        Action<SurblServerAdministrationSnapshot>? append,
        Action<SurblServerAdministrationSnapshot>? replace,
        Func<bool>? isServerAdministrator)
    {
        _server = server;
        _insert = insert;
        _update = update;
        _delete = delete;
        _append = append;
        _replace = replace;
        _isServerAdministrator = isServerAdministrator;
    }

    public bool Active { get => Snapshot.Active; set => Mutate(snapshot => snapshot with { Active = value }); }

    public int ID => Snapshot.Id;

    public string DNSHost { get => Snapshot.DnsHost; set => Mutate(snapshot => snapshot with { DnsHost = value ?? string.Empty }); }

    public string RejectMessage { get => Snapshot.RejectMessage; set => Mutate(snapshot => snapshot with { RejectMessage = value ?? string.Empty }); }

    public void Save()
    {
        EnsureServerAdministrator();
        if (Snapshot.Id == 0 && _insert is null || Snapshot.Id != 0 && _update is null)
        {
            Unavailable();
            return;
        }

        try
        {
            if (Snapshot.Id == 0)
            {
                var insertedId = _insert!(Snapshot);
                if (insertedId <= 0)
                {
                    throw new InvalidOperationException("The SURBL server insert did not return a valid generated identity.");
                }

                var saved = Snapshot with { Id = insertedId };
                _server = saved;
                _append?.Invoke(saved);
                return;
            }

            if (!_update!(Snapshot))
            {
                throw new InvalidOperationException("The SURBL server update did not affect the selected database row.");
            }

            _replace?.Invoke(Snapshot);
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the SURBL server to the database.",
                EFail);
        }
    }

    public int Score { get => Snapshot.Score; set => Mutate(snapshot => snapshot with { Score = value }); }

    public void Delete()
    {
        EnsureServerAdministrator();
        if (_delete is null)
        {
            Unavailable();
            return;
        }

        _delete(Snapshot.Id);
    }

    internal static SURBLServer CreateAuthorized(
        SurblServerAdministrationSnapshot server,
        Func<SurblServerAdministrationSnapshot, int>? insert = null,
        Func<SurblServerAdministrationSnapshot, bool>? update = null,
        Action<int>? delete = null,
        Action<SurblServerAdministrationSnapshot>? append = null,
        Action<SurblServerAdministrationSnapshot>? replace = null,
        Func<bool>? isServerAdministrator = null) =>
        new(server, insert, update, delete, append, replace, isServerAdministrator);

    private SurblServerAdministrationSnapshot Snapshot =>
        _server ?? throw new COMException(
            "SURBLServer access requires an authenticated server administrator.",
            EAccessDenied);

    private void Mutate(Func<SurblServerAdministrationSnapshot, SurblServerAdministrationSnapshot> mutation)
    {
        EnsureServerAdministrator();
        if (_insert is null && _update is null)
        {
            Unavailable();
            return;
        }

        _server = mutation(Snapshot);
    }

    private void EnsureServerAdministrator()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "SURBLServer access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "This SURBLServer member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(false)]
public static class SurblServerAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static ISurblServerAdministrationStore? _store;

    public static void Configure(ISurblServerAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static SURBLServers CreateAuthorizedAdapter(Func<bool>? isServerAdministrator = null)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer SURBL server administration runtime has not been initialized.",
                CoENotInitialized);

        IReadOnlyList<SurblServerAdministrationSnapshot> LoadServers() => store
            .GetSurblServersAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        int InsertServer(SurblServerAdministrationSnapshot server) => store
            .InsertSurblServerAsync(server, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        bool UpdateServer(SurblServerAdministrationSnapshot server) => store
            .UpdateSurblServerAsync(server, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        bool DeleteServer(int databaseId) => store
            .DeleteSurblServerByIdAsync(databaseId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return SURBLServers.CreateAuthorized(
            LoadServers(),
            LoadServers,
            InsertServer,
            UpdateServer,
            DeleteServer,
            isServerAdministrator);
    }
}
