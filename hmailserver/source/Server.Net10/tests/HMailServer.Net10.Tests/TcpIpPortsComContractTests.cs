using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class TcpIpPortsComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int DispEBadIndex = unchecked((int)0x8002000B);

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsDispatchIdsAndCompleteVtableOrder()
    {
        AssertContract(
            typeof(IInterfaceTCPIPPorts),
            "70471130-C8FA-4218-B68A-F1C9AD973FF6",
            new[]
            {
                "get_Item", "get_Count", "DeleteByDBID", "Add",
                "get_ItemByDBID", "Refresh", "SetDefault"
            });
        Assert.AreEqual(
            0,
            typeof(IInterfaceTCPIPPorts).GetProperty("Item")?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            7,
            typeof(IInterfaceTCPIPPorts).GetMethod(nameof(IInterfaceTCPIPPorts.SetDefault))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);

        AssertContract(
            typeof(IInterfaceTCPIPPort),
            "5F46B580-89DA-44A3-9518-AEEEDB80F6D7",
            new[]
            {
                "get_ID", "get_Protocol", "set_Protocol", "get_PortNumber", "set_PortNumber",
                "Save", "get_Address", "set_Address", "get_UseSSL", "set_UseSSL",
                "get_SSLCertificateID", "set_SSLCertificateID", "Delete",
                "get_ConnectionSecurity", "set_ConnectionSecurity"
            });
        Assert.AreEqual(
            9,
            typeof(IInterfaceTCPIPPort).GetProperty(nameof(IInterfaceTCPIPPort.ConnectionSecurity))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void SessionTypeEnum_PreservesLegacyValuesAndGuid()
    {
        Assert.AreEqual(new Guid("90745436-4C3F-11D9-AD17-A0BCEA20CD07"), typeof(ComSessionType).GUID);
        var values = Enum.GetNames<ComSessionType>()
            .ToDictionary(
                static name => name,
                static name => Convert.ToInt32(Enum.Parse<ComSessionType>(name)));

        Assert.AreEqual(0, values[nameof(ComSessionType.Unknown)]);
        Assert.AreEqual(1, values[nameof(ComSessionType.Smtp)]);
        Assert.AreEqual(2, values[nameof(ComSessionType.SmtpClient)]);
        Assert.AreEqual(3, values[nameof(ComSessionType.Pop3)]);
        Assert.AreEqual(4, values[nameof(ComSessionType.Pop3Client)]);
        Assert.AreEqual(5, values[nameof(ComSessionType.Imap)]);
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<TCPIPPorts>(
            "225808B4-6F03-4750-843F-3150EB1C357F",
            "hMailServer.TCPIPPorts.1",
            typeof(IInterfaceTCPIPPorts));
        AssertComClass<TCPIPPort>(
            "556DF811-3E02-4106-BCA6-C75996825E9A",
            "hMailServer.TCPIPPort.1",
            typeof(IInterfaceTCPIPPort));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var portsError = Assert.ThrowsExactly<COMException>(() => _ = new TCPIPPorts().Count);
        var portError = Assert.ThrowsExactly<COMException>(() => _ = new TCPIPPort().PortNumber);
        var settingsError = Assert.ThrowsExactly<COMException>(() => _ = new Settings().TCPIPPorts);

        Assert.AreEqual(EAccessDenied, portsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, portError.ErrorCode);
        Assert.AreEqual(EAccessDenied, settingsError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesReadOnlySnapshotsAndLegacyLookupErrors()
    {
        IInterfaceTCPIPPorts ports = TCPIPPorts.CreateAuthorized(
            new[]
            {
                Snapshot(10, ComSessionType.Smtp, 25, "0.0.0.0", ComConnectionSecurity.Tls, 100),
                Snapshot(20, ComSessionType.Imap, 143, "127.0.0.1", ComConnectionSecurity.StartTlsRequired, 0)
            });

        Assert.AreEqual(2, ports.Count);
        AssertPort(ports[0], 10, ComSessionType.Smtp, 25, "0.0.0.0", ComConnectionSecurity.Tls, true, 100);
        AssertPort(
            ports.get_ItemByDBID(20),
            20,
            ComSessionType.Imap,
            143,
            "127.0.0.1",
            ComConnectionSecurity.StartTlsRequired,
            false,
            0);

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = ports[2]);
        var badDatabaseId = Assert.ThrowsExactly<COMException>(() => _ = ports.get_ItemByDBID(30));
        var pendingAdd = Assert.ThrowsExactly<COMException>(() => ports.Add());
        var pendingDelete = Assert.ThrowsExactly<COMException>(() => ports.DeleteByDBID(10));
        var pendingRefresh = Assert.ThrowsExactly<COMException>(ports.Refresh);
        var pendingSetDefault = Assert.ThrowsExactly<COMException>(ports.SetDefault);
        var pendingMutation = Assert.ThrowsExactly<COMException>(() => ports[0].PortNumber = 2525);
        var pendingSave = Assert.ThrowsExactly<COMException>(ports[0].Save);
        var pendingPortDelete = Assert.ThrowsExactly<COMException>(ports[0].Delete);

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badDatabaseId.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAdd.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDelete.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRefresh.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingSetDefault.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingSave.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingPortDelete.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedSettings_UsesConfiguredTcpIpPortRuntime()
    {
        TcpIpPortAdministrationRuntimeHost.Configure(
            new FixedTcpIpPortAdministrationStore(
                new[]
                {
                    Snapshot(20, ComSessionType.Imap, 143, "127.0.0.1", ComConnectionSecurity.None, 0),
                    Snapshot(10, ComSessionType.Smtp, 25, "0.0.0.0", ComConnectionSecurity.None, 0)
                }));
        IInterfaceSettings settings = Settings.CreateAuthorized();

        var ports = settings.TCPIPPorts;

        Assert.AreEqual(2, ports.Count);
        Assert.AreEqual(25, ports[0].PortNumber);
        Assert.AreEqual(ComSessionType.Smtp, ports[0].Protocol);
    }

    private static TcpIpPortAdministrationSnapshot Snapshot(
        int id,
        ComSessionType protocol,
        int portNumber,
        string address,
        ComConnectionSecurity connectionSecurity,
        int sslCertificateId) =>
        new(id, (int)protocol, portNumber, address, (int)connectionSecurity, sslCertificateId);

    private static void AssertPort(
        IInterfaceTCPIPPort port,
        int id,
        ComSessionType protocol,
        int portNumber,
        string address,
        ComConnectionSecurity connectionSecurity,
        bool useSsl,
        int sslCertificateId)
    {
        Assert.AreEqual(id, port.ID);
        Assert.AreEqual(protocol, port.Protocol);
        Assert.AreEqual(portNumber, port.PortNumber);
        Assert.AreEqual(address, port.Address);
        Assert.AreEqual(useSsl, port.UseSSL);
        Assert.AreEqual(sslCertificateId, port.SSLCertificateID);
        Assert.AreEqual(connectionSecurity, port.ConnectionSecurity);
    }

    private static void AssertContract(Type contract, string interfaceId, string[] methodNames)
    {
        Assert.AreEqual(new Guid(interfaceId), contract.GUID);
        Assert.AreEqual(ComInterfaceType.InterfaceIsDual, contract.GetCustomAttribute<InterfaceTypeAttribute>()?.Value);
        Assert.AreEqual(
            TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable,
            contract.GetCustomAttribute<TypeLibTypeAttribute>()?.Value);
        CollectionAssert.AreEqual(
            methodNames,
            contract.GetMethods().OrderBy(static method => method.MetadataToken).Select(static method => method.Name).ToArray());
    }

    private static void AssertComClass<T>(string classId, string progId, Type defaultInterface)
    {
        var type = typeof(T);

        Assert.AreEqual(new Guid(classId), type.GUID);
        Assert.AreEqual(progId, type.GetCustomAttribute<ProgIdAttribute>()?.Value);
        Assert.AreEqual(ClassInterfaceType.None, type.GetCustomAttribute<ClassInterfaceAttribute>()?.Value);
        Assert.AreEqual(defaultInterface, type.GetCustomAttribute<ComDefaultInterfaceAttribute>()?.Value);
        Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes));
    }

    private sealed class FixedTcpIpPortAdministrationStore(IReadOnlyList<TcpIpPortAdministrationSnapshot> ports)
        : ITcpIpPortAdministrationStore
    {
        public ValueTask<IReadOnlyList<TcpIpPortAdministrationSnapshot>> GetTcpIpPortsAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<TcpIpPortAdministrationSnapshot>>(
                ports.OrderBy(port => port.Address, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(port => port.PortNumber)
                    .ToArray());
    }
}
