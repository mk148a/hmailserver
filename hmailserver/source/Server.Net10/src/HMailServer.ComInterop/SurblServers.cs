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
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly IReadOnlyList<SurblServerAdministrationSnapshot>? _servers;

    public SURBLServers()
    {
    }

    private SURBLServers(IReadOnlyList<SurblServerAdministrationSnapshot> servers)
    {
        _servers = servers.ToArray();
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

            return SURBLServer.CreateAuthorized(servers[index]);
        }
    }

    public void DeleteByDBID(int databaseId) => Unavailable();

    public IInterfaceSURBLServer Add() => Unavailable<IInterfaceSURBLServer>();

    public IInterfaceSURBLServer get_ItemByDBID(int databaseId)
    {
        var match = GetServers().FirstOrDefault(server => server.Id == databaseId);

        return match is null
            ? throw new COMException("No SURBL server with the specified database identifier exists.", DispEBadIndex)
            : SURBLServer.CreateAuthorized(match);
    }

    public void Refresh() => Unavailable();

    public IInterfaceSURBLServer get_ItemByDNSHost(string dnsHost)
    {
        var match = GetServers().FirstOrDefault(
            server => string.Equals(server.DnsHost, dnsHost, StringComparison.OrdinalIgnoreCase));

        return match is null ? null! : SURBLServer.CreateAuthorized(match);
    }

    internal static SURBLServers CreateAuthorized(IReadOnlyList<SurblServerAdministrationSnapshot> servers)
    {
        ArgumentNullException.ThrowIfNull(servers);
        return new SURBLServers(servers);
    }

    private IReadOnlyList<SurblServerAdministrationSnapshot> GetServers()
    {
        return _servers
            ?? throw new COMException(
                "SURBLServers access requires an authenticated server administrator.",
                EAccessDenied);
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
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly SurblServerAdministrationSnapshot? _server;

    public SURBLServer()
    {
    }

    private SURBLServer(SurblServerAdministrationSnapshot server)
    {
        _server = server;
    }

    public bool Active { get => Snapshot.Active; set => Unavailable(); }

    public int ID => Snapshot.Id;

    public string DNSHost { get => Snapshot.DnsHost; set => Unavailable(); }

    public string RejectMessage { get => Snapshot.RejectMessage; set => Unavailable(); }

    public void Save() => Unavailable();

    public int Score { get => Snapshot.Score; set => Unavailable(); }

    public void Delete() => Unavailable();

    internal static SURBLServer CreateAuthorized(SurblServerAdministrationSnapshot server) => new(server);

    private SurblServerAdministrationSnapshot Snapshot =>
        _server ?? throw new COMException(
            "SURBLServer access requires an authenticated server administrator.",
            EAccessDenied);

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

    internal static SURBLServers CreateAuthorizedAdapter()
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer SURBL server administration runtime has not been initialized.",
                CoENotInitialized);

        var servers = store
            .GetSurblServersAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return SURBLServers.CreateAuthorized(servers);
    }
}
