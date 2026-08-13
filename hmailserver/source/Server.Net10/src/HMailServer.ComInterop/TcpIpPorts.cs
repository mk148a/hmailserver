using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Net;
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
    private const int EFail = unchecked((int)0x80004005);
    private const int ELegacyComError = unchecked((int)0x800403E9);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private TcpIpPortAdministrationSnapshot[]? _ports;
    private readonly Func<IReadOnlyList<TcpIpPortAdministrationSnapshot>>? _reload;
    private readonly Func<TcpIpPortAdministrationSnapshot, int>? _insert;
    private readonly Action<int>? _deleteById;
    private readonly Action<TcpIpPortAdministrationSnapshot>? _update;
    private readonly Action? _deleteAll;
    private readonly Func<bool>? _isServerAdministrator;

    public TCPIPPorts()
    {
    }

    private TCPIPPorts(
        IReadOnlyList<TcpIpPortAdministrationSnapshot> ports,
        Func<IReadOnlyList<TcpIpPortAdministrationSnapshot>>? reload,
        Func<TcpIpPortAdministrationSnapshot, int>? insert,
        Action<int>? deleteById,
        Action<TcpIpPortAdministrationSnapshot>? update,
        Action? deleteAll,
        Func<bool>? isServerAdministrator)
    {
        _ports = ports.ToArray();
        _reload = reload;
        _insert = insert;
        _deleteById = deleteById;
        _update = update;
        _deleteAll = deleteAll;
        _isServerAdministrator = isServerAdministrator;
    }

    public int Count => GetPorts().Count;

    internal static TCPIPPorts CreateAuthorized(
        IReadOnlyList<TcpIpPortAdministrationSnapshot> ports,
        Func<IReadOnlyList<TcpIpPortAdministrationSnapshot>>? reload = null,
        Func<TcpIpPortAdministrationSnapshot, int>? insert = null,
        Action<int>? deleteById = null,
        Action<TcpIpPortAdministrationSnapshot>? update = null,
        Action? deleteAll = null,
        Func<bool>? isServerAdministrator = null)
    {
        ArgumentNullException.ThrowIfNull(ports);
        return new TCPIPPorts(ports, reload, insert, deleteById, update, deleteAll, isServerAdministrator);
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

            return TCPIPPort.CreateAuthorized(
                ports[index],
                save: _update is null ? null : SaveExistingPort,
                delete: _deleteById is null ? null : DeleteByDBID,
                isServerAdministrator: _isServerAdministrator);
        }
    }

    public IInterfaceTCPIPPort get_ItemByDBID(int databaseId)
    {
        var match = GetPorts().FirstOrDefault(port => port.Id == databaseId);

        return match is null
            ? throw new COMException("No TCP/IP port with the specified database identifier exists.", DispEBadIndex)
            : TCPIPPort.CreateAuthorized(
                match,
                save: _update is null ? null : SaveExistingPort,
                delete: _deleteById is null ? null : DeleteByDBID,
                isServerAdministrator: _isServerAdministrator);
    }

    public void DeleteByDBID(int databaseId)
    {
        var ports = GetPorts();
        if (_deleteById is null)
        {
            Unavailable();
            return;
        }

        if (!ports.Any(port => port.Id == databaseId))
        {
            return;
        }

        try
        {
            _deleteById(databaseId);
            Volatile.Write(ref _ports, ports.Where(port => port.Id != databaseId).ToArray());
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to delete the TCP/IP port from the database.",
                EFail);
        }
    }

    public IInterfaceTCPIPPort Add()
    {
        _ = GetPorts();
        if (_insert is null)
        {
            return Unavailable<IInterfaceTCPIPPort>();
        }

        return TCPIPPort.CreateAuthorized(
            new TcpIpPortAdministrationSnapshot(
                Id: 0,
                Protocol: (int)ComSessionType.Unknown,
                PortNumber: 0,
                Address: "0.0.0.0",
                ConnectionSecurity: (int)ComConnectionSecurity.None,
                SslCertificateId: 0),
            save: SaveNewPort,
            delete: DeleteByDBID,
            isServerAdministrator: _isServerAdministrator);
    }

    private TcpIpPortAdministrationSnapshot SaveNewPort(TcpIpPortAdministrationSnapshot port)
    {
        EnsureServerAdministrator();
        ValidateLegacyCertificateRequirement(port);
        var ports = GetPorts();
        if (_insert is null)
        {
            Unavailable();
        }

        try
        {
            var generatedId = _insert!(port);
            if (generatedId <= 0)
            {
                throw new InvalidOperationException(
                    "The TCP/IP port insert did not return a valid generated identity.");
            }

            var persisted = port with { Id = generatedId };
            Volatile.Write(ref _ports, ports.Append(persisted).ToArray());
            return persisted;
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the TCP/IP port to the database.",
                EFail);
        }
    }

    private TcpIpPortAdministrationSnapshot SaveExistingPort(TcpIpPortAdministrationSnapshot port)
    {
        EnsureServerAdministrator();
        ValidateLegacyCertificateRequirement(port);
        var ports = GetPorts();
        if (_update is null)
        {
            Unavailable();
        }

        try
        {
            _update!(port);
            Volatile.Write(
                ref _ports,
                ports.Select(existing => existing.Id == port.Id ? port : existing).ToArray());
            return port;
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the TCP/IP port to the database.",
                EFail);
        }
    }

    public void Refresh()
    {
        _ = GetPorts();
        if (_reload is null)
        {
            Unavailable();
            return;
        }

        try
        {
            var ports = _reload();
            ArgumentNullException.ThrowIfNull(ports);
            Volatile.Write(ref _ports, ports.ToArray());
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to retrieve a list of TCP/IP ports from the database.",
                EFail);
        }
    }

    public void SetDefault()
    {
        EnsureServerAdministrator();
        if (_reload is null)
        {
            Unavailable();
            return;
        }

        try
        {
            var refreshedPorts = _reload();
            ArgumentNullException.ThrowIfNull(refreshedPorts);
            Volatile.Write(ref _ports, refreshedPorts.ToArray());
            var ports = refreshedPorts;
            if (IsDefaultPorts(ports))
            {
                return;
            }

            if (_deleteAll is null || _insert is null)
            {
                Unavailable();
                return;
            }

            _deleteAll();
            InsertDefaultPorts();
            var reloadedPorts = _reload();
            ArgumentNullException.ThrowIfNull(reloadedPorts);
            Volatile.Write(ref _ports, reloadedPorts.ToArray());
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to reset the TCP/IP ports to their defaults.",
                EFail);
        }
    }

    private static bool IsDefaultPorts(IReadOnlyList<TcpIpPortAdministrationSnapshot> ports) =>
        ports.Count == 4
        && IsDefaultPort(ports, 0, 25, (int)ComSessionType.Smtp)
        && IsDefaultPort(ports, 1, 110, (int)ComSessionType.Pop3)
        && IsDefaultPort(ports, 2, 143, (int)ComSessionType.Imap)
        && IsDefaultPort(ports, 3, 587, (int)ComSessionType.Smtp);

    private static bool IsDefaultPort(
        IReadOnlyList<TcpIpPortAdministrationSnapshot> ports,
        int index,
        int portNumber,
        int protocol) =>
        ports[index].PortNumber == portNumber
        && ports[index].Protocol == protocol
        && ports[index].ConnectionSecurity == (int)ComConnectionSecurity.None;

    private void InsertDefaultPorts()
    {
        var defaults = new[]
        {
            (Protocol: (int)ComSessionType.Smtp, Port: 25),
            (Protocol: (int)ComSessionType.Pop3, Port: 110),
            (Protocol: (int)ComSessionType.Imap, Port: 143),
            (Protocol: (int)ComSessionType.Smtp, Port: 587)
        };

        foreach (var item in defaults)
        {
            _insert!(
                new TcpIpPortAdministrationSnapshot(
                    Id: 0,
                    Protocol: item.Protocol,
                    PortNumber: item.Port,
                    Address: "0.0.0.0",
                    ConnectionSecurity: (int)ComConnectionSecurity.None,
                    SslCertificateId: 0));
        }
    }

    private IReadOnlyList<TcpIpPortAdministrationSnapshot> GetPorts()
    {
        return Volatile.Read(ref _ports)
            ?? throw new COMException(
                "TCPIPPorts access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private void EnsureServerAdministrator()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "TCPIPPorts access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private static void ValidateLegacyCertificateRequirement(TcpIpPortAdministrationSnapshot port)
    {
        if (port.SslCertificateId == 0 && port.ConnectionSecurity is
            (int)ComConnectionSecurity.Tls or
            (int)ComConnectionSecurity.StartTlsOptional or
            (int)ComConnectionSecurity.StartTlsRequired)
        {
            throw new COMException(
                "Certificate must be specified.",
                ELegacyComError);
        }
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
    private const int ELegacyComError = unchecked((int)0x800403E9);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private TcpIpPortAdministrationSnapshot? _port;
    private readonly Func<TcpIpPortAdministrationSnapshot, TcpIpPortAdministrationSnapshot>? _save;
    private readonly Action<int>? _delete;
    private readonly Func<bool>? _isServerAdministrator;

    public TCPIPPort()
    {
    }

    private TCPIPPort(
        TcpIpPortAdministrationSnapshot port,
        Func<TcpIpPortAdministrationSnapshot, TcpIpPortAdministrationSnapshot>? save,
        Action<int>? delete,
        Func<bool>? isServerAdministrator)
    {
        _port = port;
        _save = save;
        _delete = delete;
        _isServerAdministrator = isServerAdministrator;
    }

    public int ID => Snapshot.Id;

    public ComSessionType Protocol
    {
        get => (ComSessionType)Snapshot.Protocol;
        set => Mutate(snapshot => snapshot with { Protocol = (int)value });
    }

    public int PortNumber { get => Snapshot.PortNumber; set => Mutate(snapshot => snapshot with { PortNumber = value }); }

    public string Address
    {
        get => Snapshot.Address;
        set
        {
            if (!IPAddress.TryParse(value ?? string.Empty, out var address))
            {
                throw new COMException("Invalid IP address string.", ELegacyComError);
            }

            Mutate(snapshot => snapshot with { Address = address.ToString() });
        }
    }

    public bool UseSSL
    {
        get => Snapshot.ConnectionSecurity == (int)ComConnectionSecurity.Tls;
        set => Mutate(snapshot => snapshot with
        {
            ConnectionSecurity = value
                ? (int)ComConnectionSecurity.Tls
                : (int)ComConnectionSecurity.None
        });
    }

    public int SSLCertificateID
    {
        get => Snapshot.SslCertificateId;
        set => Mutate(snapshot => snapshot with { SslCertificateId = value });
    }

    public ComConnectionSecurity ConnectionSecurity
    {
        get => (ComConnectionSecurity)Snapshot.ConnectionSecurity;
        set => Mutate(snapshot => snapshot with { ConnectionSecurity = (int)value });
    }

    internal static TCPIPPort CreateAuthorized(
        TcpIpPortAdministrationSnapshot port,
        Func<TcpIpPortAdministrationSnapshot, TcpIpPortAdministrationSnapshot>? save = null,
        Action<int>? delete = null,
        Func<bool>? isServerAdministrator = null) =>
        new(port, save, delete, isServerAdministrator);

    public void Save()
    {
        EnsureServerAdministrator();
        if (_save is null)
        {
            Unavailable();
            return;
        }

        _port = _save(Snapshot);
    }

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

    private TcpIpPortAdministrationSnapshot Snapshot =>
        _port ?? throw new COMException(
            "TCPIPPort access requires an authenticated server administrator.",
            EAccessDenied);

    private void Mutate(Func<TcpIpPortAdministrationSnapshot, TcpIpPortAdministrationSnapshot> mutation)
    {
        EnsureServerAdministrator();
        if (_save is null)
        {
            Unavailable();
            return;
        }

        _port = mutation(Snapshot);
    }

    private void EnsureServerAdministrator()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "TCPIPPort access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

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

    internal static TCPIPPorts CreateAuthorizedAdapter(Func<bool>? isServerAdministrator = null)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer TCP/IP port administration runtime has not been initialized.",
                CoENotInitialized);

        IReadOnlyList<TcpIpPortAdministrationSnapshot> LoadPorts() => store
            .GetTcpIpPortsAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        int InsertPort(TcpIpPortAdministrationSnapshot port) => store
            .InsertTcpIpPortAsync(port, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        void DeletePort(int databaseId) => store
            .DeleteTcpIpPortByIdAsync(databaseId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        void UpdatePort(TcpIpPortAdministrationSnapshot port) => store
            .UpdateTcpIpPortAsync(port, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        void DeleteAllPorts() => store
            .DeleteAllTcpIpPortsAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return TCPIPPorts.CreateAuthorized(
            LoadPorts(),
            LoadPorts,
            InsertPort,
            DeletePort,
            UpdatePort,
            DeleteAllPorts,
            isServerAdministrator);
    }
}
