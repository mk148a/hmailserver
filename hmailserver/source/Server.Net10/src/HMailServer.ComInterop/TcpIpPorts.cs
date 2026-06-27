using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("70471130-C8FA-4218-B68A-F1C9AD973FF6")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceTCPIPPorts
{
    [DispId(0)]
    IInterfaceTCPIPPort this[int index] { get; }

    [DispId(1)]
    int Count { get; }

    [DispId(2)]
    void DeleteByDBID(int databaseId);

    [DispId(3)]
    IInterfaceTCPIPPort Add();

    [DispId(5)]
    [SpecialName]
    IInterfaceTCPIPPort get_ItemByDBID(int databaseId);

    [DispId(6)]
    void Refresh();

    [DispId(7)]
    void SetDefault();
}

[ComVisible(true)]
[Guid("5F46B580-89DA-44A3-9518-AEEEDB80F6D7")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceTCPIPPort
{
    [DispId(1)]
    int ID { get; }

    [DispId(2)]
    ComSessionType Protocol { get; set; }

    [DispId(3)]
    int PortNumber { get; set; }

    [DispId(4)]
    void Save();

    [DispId(5)]
    string Address { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(6)]
    bool UseSSL
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(7)]
    int SSLCertificateID { get; set; }

    [DispId(8)]
    void Delete();

    [DispId(9)]
    ComConnectionSecurity ConnectionSecurity { get; set; }
}

[ComVisible(true)]
[Guid("225808B4-6F03-4750-843F-3150EB1C357F")]
[ProgId("hMailServer.TCPIPPorts.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceTCPIPPorts))]
public sealed class TCPIPPorts : IInterfaceTCPIPPorts
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly IReadOnlyList<TcpIpPortAdministrationSnapshot>? _ports;

    public TCPIPPorts()
    {
    }

    private TCPIPPorts(IReadOnlyList<TcpIpPortAdministrationSnapshot> ports)
    {
        _ports = ports.ToArray();
    }

    public int Count => GetPorts().Count;

    internal static TCPIPPorts CreateAuthorized(IReadOnlyList<TcpIpPortAdministrationSnapshot> ports)
    {
        ArgumentNullException.ThrowIfNull(ports);
        return new TCPIPPorts(ports);
    }

    public IInterfaceTCPIPPort this[int index]
    {
        get
        {
            var ports = GetPorts();
            if (index < 0 || index >= ports.Count)
            {
                throw new COMException("TCP/IP port index was outside the collection.", DispEBadIndex);
            }

            return TCPIPPort.CreateAuthorized(ports[index]);
        }
    }

    public IInterfaceTCPIPPort get_ItemByDBID(int databaseId)
    {
        var match = GetPorts().FirstOrDefault(port => port.Id == databaseId);

        return match is null
            ? throw new COMException("No TCP/IP port with the specified database identifier exists.", DispEBadIndex)
            : TCPIPPort.CreateAuthorized(match);
    }

    public void DeleteByDBID(int databaseId) => Unavailable();

    public IInterfaceTCPIPPort Add() => Unavailable<IInterfaceTCPIPPort>();

    public void Refresh() => Unavailable();

    public void SetDefault() => Unavailable();

    private IReadOnlyList<TcpIpPortAdministrationSnapshot> GetPorts()
    {
        return _ports
            ?? throw new COMException(
                "TCPIPPorts access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private T Unavailable<T>()
    {
        _ = GetPorts();
        throw new COMException(
            "This TCPIPPorts member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Unavailable()
    {
        _ = GetPorts();
        throw new COMException(
            "This TCPIPPorts member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(true)]
[Guid("556DF811-3E02-4106-BCA6-C75996825E9A")]
[ProgId("hMailServer.TCPIPPort.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceTCPIPPort))]
public sealed class TCPIPPort : IInterfaceTCPIPPort
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly TcpIpPortAdministrationSnapshot? _port;

    public TCPIPPort()
    {
    }

    private TCPIPPort(TcpIpPortAdministrationSnapshot port)
    {
        _port = port;
    }

    public int ID => Snapshot.Id;

    public ComSessionType Protocol { get => (ComSessionType)Snapshot.Protocol; set => Unavailable(); }

    public int PortNumber { get => Snapshot.PortNumber; set => Unavailable(); }

    public string Address { get => Snapshot.Address; set => Unavailable(); }

    public bool UseSSL { get => Snapshot.ConnectionSecurity == (int)ComConnectionSecurity.Tls; set => Unavailable(); }

    public int SSLCertificateID { get => Snapshot.SslCertificateId; set => Unavailable(); }

    public ComConnectionSecurity ConnectionSecurity
    {
        get => (ComConnectionSecurity)Snapshot.ConnectionSecurity;
        set => Unavailable();
    }

    internal static TCPIPPort CreateAuthorized(TcpIpPortAdministrationSnapshot port) => new(port);

    public void Save() => Unavailable();

    public void Delete() => Unavailable();

    private TcpIpPortAdministrationSnapshot Snapshot =>
        _port ?? throw new COMException(
            "TCPIPPort access requires an authenticated server administrator.",
            EAccessDenied);

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "This TCPIPPort member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(false)]
public static class TcpIpPortAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static ITcpIpPortAdministrationStore? _store;

    public static void Configure(ITcpIpPortAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static TCPIPPorts CreateAuthorizedAdapter()
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer TCP/IP port administration runtime has not been initialized.",
                CoENotInitialized);

        var ports = store
            .GetTcpIpPortsAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return TCPIPPorts.CreateAuthorized(ports);
    }
}
