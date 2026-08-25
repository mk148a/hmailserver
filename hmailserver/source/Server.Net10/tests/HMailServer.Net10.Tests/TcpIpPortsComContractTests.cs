using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class TcpIpPortsComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ELegacyComError = unchecked((int)0x800403E9);
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
        var portsRefreshError = Assert.ThrowsExactly<COMException>(new TCPIPPorts().Refresh);
        var portError = Assert.ThrowsExactly<COMException>(() => _ = new TCPIPPort().PortNumber);
        var settingsError = Assert.ThrowsExactly<COMException>(() => _ = new Settings().TCPIPPorts);

        Assert.AreEqual(EAccessDenied, portsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, portsRefreshError.ErrorCode);
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
    public void AuthorizedCollection_RefreshAtomicallyReplacesSnapshotAndRetainsItOnFailure()
    {
        var failReload = false;
        var reloads = 0;
        IInterfaceTCPIPPorts ports = TCPIPPorts.CreateAuthorized(
            new[]
            {
                Snapshot(10, ComSessionType.Smtp, 25, "0.0.0.0", ComConnectionSecurity.Tls, 100)
            },
            () =>
            {
                reloads++;
                if (failReload)
                {
                    throw new InvalidOperationException("Simulated store failure.");
                }

                return new[]
                {
                    Snapshot(20, ComSessionType.Imap, 143, "127.0.0.1", ComConnectionSecurity.StartTlsRequired, 0),
                    Snapshot(30, ComSessionType.Smtp, 2525, "0.0.0.0", ComConnectionSecurity.None, 0)
                };
            });

        Assert.AreEqual(1, ports.Count);
        Assert.AreEqual(25, ports[0].PortNumber);

        ports.Refresh();

        Assert.AreEqual(1, reloads);
        Assert.AreEqual(2, ports.Count);
        AssertPort(
            ports[0],
            20,
            ComSessionType.Imap,
            143,
            "127.0.0.1",
            ComConnectionSecurity.StartTlsRequired,
            false,
            0);
        Assert.AreEqual(2525, ports.get_ItemByDBID(30).PortNumber);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = ports.get_ItemByDBID(10)).ErrorCode);

        failReload = true;
        var refreshFailure = Assert.ThrowsExactly<COMException>(ports.Refresh);

        Assert.AreEqual(EFail, refreshFailure.ErrorCode);
        Assert.AreEqual(2, reloads);
        Assert.AreEqual(2, ports.Count);
        Assert.AreEqual(143, ports.get_ItemByDBID(20).PortNumber);
    }

    [TestMethod]
    public void AuthorizedSettings_UsesConfiguredTcpIpPortRuntime()
    {
        var store = new MutableTcpIpPortAdministrationStore(
            new[]
            {
                Snapshot(20, ComSessionType.Imap, 143, "127.0.0.1", ComConnectionSecurity.None, 0),
                Snapshot(10, ComSessionType.Smtp, 25, "0.0.0.0", ComConnectionSecurity.None, 0)
            });
        TcpIpPortAdministrationRuntimeHost.Configure(store);
        IInterfaceSettings settings = Settings.CreateAuthorized();

        var ports = settings.TCPIPPorts;

        Assert.AreEqual(2, ports.Count);
        Assert.AreEqual(25, ports[0].PortNumber);
        Assert.AreEqual(ComSessionType.Smtp, ports[0].Protocol);
        Assert.AreEqual(1, store.ReadCount);

        store.Replace(
            new[]
            {
                Snapshot(30, ComSessionType.Smtp, 2525, "0.0.0.0", ComConnectionSecurity.StartTlsRequired, 0),
                Snapshot(20, ComSessionType.Imap, 143, "127.0.0.1", ComConnectionSecurity.None, 0)
            });

        ports.Refresh();

        Assert.AreEqual(2, store.ReadCount);
        Assert.AreEqual(2, ports.Count);
        Assert.AreEqual(2525, ports[0].PortNumber);
        Assert.AreEqual(30, ports.get_ItemByDBID(30).ID);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = ports.get_ItemByDBID(10)).ErrorCode);
    }

    [TestMethod]
    public void AuthorizedSettings_AddStagesNewPortAndPublishesAfterInsert()
    {
        var store = new MutableTcpIpPortAdministrationStore(
            new[] { Snapshot(10, ComSessionType.Smtp, 25, "0.0.0.0", ComConnectionSecurity.None, 0) });
        TcpIpPortAdministrationRuntimeHost.Configure(store);
        IInterfaceSettings settings = Settings.CreateAuthorized(isServerAdministrator: static () => true);
        var ports = settings.TCPIPPorts;
        var pending = ports.Add();

        Assert.AreEqual(0, pending.ID);
        Assert.AreEqual(ComSessionType.Unknown, pending.Protocol);
        Assert.AreEqual(0, pending.PortNumber);
        Assert.AreEqual("0.0.0.0", pending.Address);

        pending.Protocol = ComSessionType.Imap;
        pending.PortNumber = 993;
        pending.Address = "2001:db8::1";
        var invalidAddress = Assert.ThrowsExactly<COMException>(() => pending.Address = "not-an-ip");
        pending.ConnectionSecurity = ComConnectionSecurity.StartTlsRequired;
        pending.SSLCertificateID = 42;

        Assert.AreEqual(ELegacyComError, invalidAddress.ErrorCode);
        Assert.AreEqual("2001:db8::1", pending.Address);

        pending.Save();

        Assert.AreEqual(1, store.InsertedPorts.Count);
        var inserted = store.InsertedPorts[0];
        Assert.AreEqual(0, inserted.Id);
        Assert.AreEqual((int)ComSessionType.Imap, inserted.Protocol);
        Assert.AreEqual(993, inserted.PortNumber);
        Assert.AreEqual("2001:db8::1", inserted.Address);
        Assert.AreEqual((int)ComConnectionSecurity.StartTlsRequired, inserted.ConnectionSecurity);
        Assert.AreEqual(42, inserted.SslCertificateId);
        Assert.AreEqual(30, pending.ID);
        Assert.AreEqual(2, ports.Count);
        Assert.AreEqual(30, ports[1].ID);
    }

    [TestMethod]
    public void AuthorizedSettings_NewPortSaveFailureRetainsDraftAndAllowsRetry()
    {
        var store = new MutableTcpIpPortAdministrationStore(Array.Empty<TcpIpPortAdministrationSnapshot>())
        {
            FailInsert = true
        };
        TcpIpPortAdministrationRuntimeHost.Configure(store);
        IInterfaceSettings settings = Settings.CreateAuthorized(isServerAdministrator: static () => true);
        var ports = settings.TCPIPPorts;
        var pending = ports.Add();
        pending.PortNumber = 2525;

        var error = Assert.ThrowsExactly<COMException>(pending.Save);

        Assert.AreEqual(EFail, error.ErrorCode);
        Assert.AreEqual(0, pending.ID);
        Assert.AreEqual(2525, pending.PortNumber);
        Assert.AreEqual(0, ports.Count);

        store.FailInsert = false;
        pending.Save();

        Assert.AreEqual(30, pending.ID);
        Assert.AreEqual(1, ports.Count);
    }

    [TestMethod]
    public void AuthorizedSettings_NewPortSaveRejectsTlsWithoutCertificateLikeLegacy()
    {
        var store = new MutableTcpIpPortAdministrationStore(Array.Empty<TcpIpPortAdministrationSnapshot>());
        TcpIpPortAdministrationRuntimeHost.Configure(store);
        IInterfaceSettings settings = Settings.CreateAuthorized(isServerAdministrator: static () => true);
        var ports = settings.TCPIPPorts;
        var pending = ports.Add();
        pending.ConnectionSecurity = ComConnectionSecurity.StartTlsRequired;

        var error = Assert.ThrowsExactly<COMException>(pending.Save);

        Assert.AreEqual(ELegacyComError, error.ErrorCode);
        Assert.AreEqual(0, pending.ID);
        Assert.AreEqual(ComConnectionSecurity.StartTlsRequired, pending.ConnectionSecurity);
        Assert.AreEqual(0, store.InsertedPorts.Count);
        Assert.AreEqual(0, ports.Count);
    }

    [TestMethod]
    public void AuthorizedSettings_ExistingPortSaveRejectsTlsWithoutCertificateAndRetainsSnapshot()
    {
        var store = new MutableTcpIpPortAdministrationStore(
            new[] { Snapshot(10, ComSessionType.Smtp, 25, "0.0.0.0", ComConnectionSecurity.None, 0) });
        TcpIpPortAdministrationRuntimeHost.Configure(store);
        IInterfaceSettings settings = Settings.CreateAuthorized(isServerAdministrator: static () => true);
        var ports = settings.TCPIPPorts;
        var port = ports[0];
        port.ConnectionSecurity = ComConnectionSecurity.StartTlsOptional;

        var error = Assert.ThrowsExactly<COMException>(port.Save);

        Assert.AreEqual(ELegacyComError, error.ErrorCode);
        Assert.AreEqual(ComConnectionSecurity.StartTlsOptional, port.ConnectionSecurity);
        Assert.AreEqual(ComConnectionSecurity.None, ports[0].ConnectionSecurity);
        Assert.AreEqual(0, store.UpdatedPorts.Count);
    }

    [TestMethod]
    public void AuthorizedSettings_RetainedNewPortSaveRechecksServerAdministrator()
    {
        var isServerAdministrator = true;
        var store = new MutableTcpIpPortAdministrationStore(Array.Empty<TcpIpPortAdministrationSnapshot>());
        TcpIpPortAdministrationRuntimeHost.Configure(store);
        IInterfaceSettings settings = Settings.CreateAuthorized(
            isServerAdministrator: () => isServerAdministrator);
        var pending = settings.TCPIPPorts.Add();

        isServerAdministrator = false;
        var error = Assert.ThrowsExactly<COMException>(pending.Save);

        Assert.AreEqual(EAccessDenied, error.ErrorCode);
        Assert.AreEqual(0, store.InsertedPorts.Count);
        isServerAdministrator = true;
        Assert.AreEqual(0, pending.ID);
    }

    [TestMethod]
    public void AuthorizedSettings_DeleteByDBIDUsesOwningSnapshotAndStaleItemsNoOp()
    {
        var store = new MutableTcpIpPortAdministrationStore(
            new[]
            {
                Snapshot(10, ComSessionType.Smtp, 25, "0.0.0.0", ComConnectionSecurity.None, 0),
                Snapshot(20, ComSessionType.Imap, 143, "127.0.0.1", ComConnectionSecurity.None, 0)
            });
        TcpIpPortAdministrationRuntimeHost.Configure(store);
        IInterfaceSettings settings = Settings.CreateAuthorized(isServerAdministrator: static () => true);
        var ports = settings.TCPIPPorts;
        var retained = ports.get_ItemByDBID(20);

        ports.DeleteByDBID(999);
        ports.DeleteByDBID(20);
        retained.Delete();

        CollectionAssert.AreEqual(new[] { 20 }, store.DeletedIds);
        Assert.AreEqual(1, ports.Count);
        Assert.AreEqual(10, ports[0].ID);
    }

    [TestMethod]
    public void AuthorizedSettings_DeleteFailureMapsToComFailureAndRetainsSnapshot()
    {
        var store = new MutableTcpIpPortAdministrationStore(
            new[] { Snapshot(10, ComSessionType.Smtp, 25, "0.0.0.0", ComConnectionSecurity.None, 0) })
        {
            FailDelete = true
        };
        TcpIpPortAdministrationRuntimeHost.Configure(store);
        IInterfaceSettings settings = Settings.CreateAuthorized(isServerAdministrator: static () => true);
        var ports = settings.TCPIPPorts;

        var error = Assert.ThrowsExactly<COMException>(() => ports.DeleteByDBID(10));

        Assert.AreEqual(EFail, error.ErrorCode);
        CollectionAssert.AreEqual(new[] { 10 }, store.DeletedIds);
        Assert.AreEqual(1, ports.Count);
    }

    [TestMethod]
    public void AuthorizedSettings_RetainedPortDeleteRechecksServerAdministrator()
    {
        var isServerAdministrator = true;
        var store = new MutableTcpIpPortAdministrationStore(
            new[] { Snapshot(10, ComSessionType.Smtp, 25, "0.0.0.0", ComConnectionSecurity.None, 0) });
        TcpIpPortAdministrationRuntimeHost.Configure(store);
        IInterfaceSettings settings = Settings.CreateAuthorized(
            isServerAdministrator: () => isServerAdministrator);
        var port = settings.TCPIPPorts[0];

        isServerAdministrator = false;
        var error = Assert.ThrowsExactly<COMException>(port.Delete);

        Assert.AreEqual(EAccessDenied, error.ErrorCode);
        Assert.AreEqual(0, store.DeletedIds.Count);
    }

    [TestMethod]
    public void AuthorizedSettings_ExistingPortSaveUsesStoreAndPublishesSnapshot()
    {
        var store = new MutableTcpIpPortAdministrationStore(
            new[] { Snapshot(10, ComSessionType.Smtp, 25, "0.0.0.0", ComConnectionSecurity.None, 0) });
        TcpIpPortAdministrationRuntimeHost.Configure(store);
        IInterfaceSettings settings = Settings.CreateAuthorized(isServerAdministrator: static () => true);
        var ports = settings.TCPIPPorts;
        var port = ports[0];

        port.Protocol = ComSessionType.Imap;
        port.PortNumber = 993;
        port.Address = "127.0.0.1";
        port.ConnectionSecurity = ComConnectionSecurity.StartTlsRequired;
        port.SSLCertificateID = 42;
        port.Save();

        Assert.AreEqual(1, store.UpdatedPorts.Count);
        var updated = store.UpdatedPorts[0];
        Assert.AreEqual(10, updated.Id);
        Assert.AreEqual((int)ComSessionType.Imap, updated.Protocol);
        Assert.AreEqual(993, updated.PortNumber);
        Assert.AreEqual("127.0.0.1", updated.Address);
        Assert.AreEqual((int)ComConnectionSecurity.StartTlsRequired, updated.ConnectionSecurity);
        Assert.AreEqual(42, updated.SslCertificateId);
        Assert.AreEqual(993, ports[0].PortNumber);
    }

    [TestMethod]
    public void AuthorizedSettings_ExistingPortSaveFailureRetainsParentSnapshot()
    {
        var store = new MutableTcpIpPortAdministrationStore(
            new[] { Snapshot(10, ComSessionType.Smtp, 25, "0.0.0.0", ComConnectionSecurity.None, 0) })
        {
            FailUpdate = true
        };
        TcpIpPortAdministrationRuntimeHost.Configure(store);
        IInterfaceSettings settings = Settings.CreateAuthorized(isServerAdministrator: static () => true);
        var ports = settings.TCPIPPorts;
        var port = ports[0];
        port.PortNumber = 2525;

        var error = Assert.ThrowsExactly<COMException>(port.Save);

        Assert.AreEqual(EFail, error.ErrorCode);
        Assert.AreEqual(2525, port.PortNumber);
        Assert.AreEqual(25, ports[0].PortNumber);
        Assert.AreEqual(1, store.UpdatedPorts.Count);
    }

    [TestMethod]
    public void SetDefault_NoOpsWhenPortsAlreadyMatchLegacyDefaults()
    {
        var deleteAllCalls = 0;
        var insertCalls = 0;
        IInterfaceTCPIPPorts ports = TCPIPPorts.CreateAuthorized(
            new[]
            {
                Snapshot(1, ComSessionType.Smtp, 25, "0.0.0.0", ComConnectionSecurity.None, 0),
                Snapshot(2, ComSessionType.Pop3, 110, "0.0.0.0", ComConnectionSecurity.None, 0),
                Snapshot(3, ComSessionType.Imap, 143, "0.0.0.0", ComConnectionSecurity.None, 0),
                Snapshot(4, ComSessionType.Smtp, 587, "0.0.0.0", ComConnectionSecurity.None, 0)
            },
            reload: () => new[]
            {
                Snapshot(1, ComSessionType.Smtp, 25, "0.0.0.0", ComConnectionSecurity.None, 0),
                Snapshot(2, ComSessionType.Pop3, 110, "0.0.0.0", ComConnectionSecurity.None, 0),
                Snapshot(3, ComSessionType.Imap, 143, "0.0.0.0", ComConnectionSecurity.None, 0),
                Snapshot(4, ComSessionType.Smtp, 587, "0.0.0.0", ComConnectionSecurity.None, 0)
            },
            insert: _ =>
            {
                insertCalls++;
                return insertCalls;
            },
            deleteAll: () => deleteAllCalls++);

        ports.SetDefault();

        Assert.AreEqual(0, deleteAllCalls);
        Assert.AreEqual(0, insertCalls);
        Assert.AreEqual(4, ports.Count);
    }

    [TestMethod]
    public void SetDefault_NoOpsWhenLegacyDefaultsUseSpecificAddresses()
    {
        var deleteAllCalls = 0;
        var insertCalls = 0;
        IInterfaceTCPIPPorts ports = TCPIPPorts.CreateAuthorized(
            new[]
            {
                Snapshot(1, ComSessionType.Smtp, 25, "127.0.0.1", ComConnectionSecurity.None, 0),
                Snapshot(2, ComSessionType.Pop3, 110, "127.0.0.1", ComConnectionSecurity.None, 0),
                Snapshot(3, ComSessionType.Imap, 143, "127.0.0.1", ComConnectionSecurity.None, 0),
                Snapshot(4, ComSessionType.Smtp, 587, "127.0.0.1", ComConnectionSecurity.None, 0)
            },
            reload: () => new[]
            {
                Snapshot(1, ComSessionType.Smtp, 25, "127.0.0.1", ComConnectionSecurity.None, 0),
                Snapshot(2, ComSessionType.Pop3, 110, "127.0.0.1", ComConnectionSecurity.None, 0),
                Snapshot(3, ComSessionType.Imap, 143, "127.0.0.1", ComConnectionSecurity.None, 0),
                Snapshot(4, ComSessionType.Smtp, 587, "127.0.0.1", ComConnectionSecurity.None, 0)
            },
            insert: _ => ++insertCalls,
            deleteAll: () => deleteAllCalls++);

        ports.SetDefault();

        Assert.AreEqual(0, deleteAllCalls);
        Assert.AreEqual(0, insertCalls);
        Assert.AreEqual("127.0.0.1", ports[0].Address);
    }

    [TestMethod]
    public void SetDefault_RefreshesBeforeComparingAndMapsRefreshFailure()
    {
        var reloadCalls = 0;
        IInterfaceTCPIPPorts ports = TCPIPPorts.CreateAuthorized(
            new[]
            {
                Snapshot(1, ComSessionType.Smtp, 25, "0.0.0.0", ComConnectionSecurity.None, 0),
                Snapshot(2, ComSessionType.Pop3, 110, "0.0.0.0", ComConnectionSecurity.None, 0),
                Snapshot(3, ComSessionType.Imap, 143, "0.0.0.0", ComConnectionSecurity.None, 0),
                Snapshot(4, ComSessionType.Smtp, 587, "0.0.0.0", ComConnectionSecurity.None, 0)
            },
            reload: () =>
            {
                reloadCalls++;
                throw new InvalidOperationException("Simulated refresh failure.");
            });

        var error = Assert.ThrowsExactly<COMException>(ports.SetDefault);

        Assert.AreEqual(EFail, error.ErrorCode);
        Assert.AreEqual(1, reloadCalls);
        Assert.AreEqual(4, ports.Count);
        Assert.AreEqual(25, ports[0].PortNumber);
    }

    [TestMethod]
    public void SetDefault_ResetsNonDefaultPortsToLegacyDefaults()
    {
        var deleteAllCalls = 0;
        var reloadCalls = 0;
        var inserted = new List<TcpIpPortAdministrationSnapshot>();
        var nextId = 100;
        IInterfaceTCPIPPorts ports = TCPIPPorts.CreateAuthorized(
            new[] { Snapshot(10, ComSessionType.Imap, 143, "127.0.0.1", ComConnectionSecurity.StartTlsRequired, 0) },
            reload: () =>
            {
                reloadCalls++;
                return reloadCalls == 1
                    ? new[] { Snapshot(10, ComSessionType.Imap, 143, "127.0.0.1", ComConnectionSecurity.StartTlsRequired, 0) }
                    : new[]
                    {
                        new TcpIpPortAdministrationSnapshot(100, (int)ComSessionType.Smtp, 25, "0.0.0.0", 0, 0),
                        new TcpIpPortAdministrationSnapshot(101, (int)ComSessionType.Pop3, 110, "0.0.0.0", 0, 0),
                        new TcpIpPortAdministrationSnapshot(102, (int)ComSessionType.Imap, 143, "0.0.0.0", 0, 0),
                        new TcpIpPortAdministrationSnapshot(103, (int)ComSessionType.Smtp, 587, "0.0.0.0", 0, 0)
                    };
            },
            insert: port =>
            {
                inserted.Add(port);
                return ++nextId;
            },
            deleteAll: () => deleteAllCalls++);

        ports.SetDefault();

        Assert.AreEqual(1, deleteAllCalls);
        Assert.AreEqual(4, inserted.Count);
        CollectionAssert.AreEqual(
            new[] { (int)ComSessionType.Smtp, (int)ComSessionType.Pop3, (int)ComSessionType.Imap, (int)ComSessionType.Smtp },
            inserted.Select(port => port.Protocol).ToArray());
        CollectionAssert.AreEqual(
            new[] { 25, 110, 143, 587 },
            inserted.Select(port => port.PortNumber).ToArray());
        Assert.IsTrue(inserted.All(port => port.Address == "0.0.0.0"));
        Assert.IsTrue(inserted.All(port => port.ConnectionSecurity == (int)ComConnectionSecurity.None));
        Assert.AreEqual(4, ports.Count);
        Assert.AreEqual(25, ports[0].PortNumber);
    }

    [TestMethod]
    public void CollectionMutations_HoldAuthorizationLeaseAcrossStoreCallbacks()
    {
        var activeLeases = 0;
        var disposedLeases = 0;
        var inserts = 0;
        var updates = 0;
        var deletes = 0;

        Func<CancellationToken, ValueTask<IDisposable?>> leaseFactory = _ =>
        {
            activeLeases++;
            return ValueTask.FromResult<IDisposable?>(new TrackingLease(() =>
            {
                activeLeases--;
                disposedLeases++;
            }));
        };

        IInterfaceTCPIPPorts ports = TCPIPPorts.CreateAuthorized(
            new[] { Snapshot(10, ComSessionType.Smtp, 25, "0.0.0.0", ComConnectionSecurity.None, 0) },
            insert: _ =>
            {
                Assert.AreEqual(1, activeLeases);
                inserts++;
                return 20;
            },
            update: _ =>
            {
                Assert.AreEqual(1, activeLeases);
                updates++;
            },
            deleteById: _ =>
            {
                Assert.AreEqual(1, activeLeases);
                deletes++;
            },
            isServerAdministrator: static () => true,
            authorizationLeaseFactory: leaseFactory);

        var draft = ports.Add();
        draft.PortNumber = 2525;
        draft.Save();
        var existing = ports[0];
        existing.PortNumber = 2626;
        existing.Save();
        existing.Delete();

        Assert.AreEqual(1, inserts);
        Assert.AreEqual(1, updates);
        Assert.AreEqual(1, deletes);
        Assert.AreEqual(0, activeLeases);
        Assert.AreEqual(3, disposedLeases);
    }

    [TestMethod]
    public void CollectionMutations_DenyBeforeStoreWhenAuthorizationLeaseIsUnavailable()
    {
        var leaseRequests = 0;
        var stores = 0;
        IInterfaceTCPIPPorts ports = TCPIPPorts.CreateAuthorized(
            new[] { Snapshot(10, ComSessionType.Smtp, 25, "0.0.0.0", ComConnectionSecurity.None, 0) },
            insert: _ =>
            {
                stores++;
                return 20;
            },
            update: _ => stores++,
            deleteById: _ => stores++,
            isServerAdministrator: static () => true,
            authorizationLeaseFactory: _ =>
            {
                leaseRequests++;
                return ValueTask.FromResult<IDisposable?>(null);
            });

        var draft = ports.Add();
        var insertError = Assert.ThrowsExactly<COMException>(draft.Save);
        var existing = ports[0];
        existing.PortNumber = 2525;
        var updateError = Assert.ThrowsExactly<COMException>(existing.Save);
        var deleteError = Assert.ThrowsExactly<COMException>(() => ports.DeleteByDBID(10));

        Assert.AreEqual(EAccessDenied, insertError.ErrorCode);
        Assert.AreEqual(EAccessDenied, updateError.ErrorCode);
        Assert.AreEqual(EAccessDenied, deleteError.ErrorCode);
        Assert.AreEqual(0, stores);
        Assert.AreEqual(3, leaseRequests);
    }

    [TestMethod]
    public void SetDefault_MapsStoreFailureToEFailAndRetainsSnapshot()
    {
        IInterfaceTCPIPPorts ports = TCPIPPorts.CreateAuthorized(
            new[] { Snapshot(10, ComSessionType.Imap, 143, "127.0.0.1", ComConnectionSecurity.None, 0) },
            reload: () => throw new InvalidOperationException("Simulated store failure."),
            insert: _ => 1,
            deleteAll: () => { });

        var failure = Assert.ThrowsExactly<COMException>(ports.SetDefault);

        Assert.AreEqual(unchecked((int)0x80004005), failure.ErrorCode);
        Assert.AreEqual(1, ports.Count);
    }

    [TestMethod]
    public void SetDefault_RechecksLiveServerAdministrator()
    {
        var authenticated = true;
        IInterfaceTCPIPPorts ports = TCPIPPorts.CreateAuthorized(
            new[] { Snapshot(10, ComSessionType.Imap, 143, "127.0.0.1", ComConnectionSecurity.None, 0) },
            isServerAdministrator: () => authenticated);

        authenticated = false;
        var denied = Assert.ThrowsExactly<COMException>(ports.SetDefault);

        Assert.AreEqual(unchecked((int)0x80070005), denied.ErrorCode);
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

    private sealed class TrackingLease(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
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

    private sealed class MutableTcpIpPortAdministrationStore(IReadOnlyList<TcpIpPortAdministrationSnapshot> ports)
        : ITcpIpPortAdministrationStore
    {
        private IReadOnlyList<TcpIpPortAdministrationSnapshot> _ports = ports;

        public int ReadCount { get; private set; }

        public bool FailInsert { get; set; }

        public bool FailDelete { get; set; }

        public bool FailUpdate { get; set; }

        public List<TcpIpPortAdministrationSnapshot> InsertedPorts { get; } = [];

        public List<int> DeletedIds { get; } = [];

        public List<TcpIpPortAdministrationSnapshot> UpdatedPorts { get; } = [];

        public void Replace(IReadOnlyList<TcpIpPortAdministrationSnapshot> ports)
        {
            _ports = ports;
        }

        public ValueTask<IReadOnlyList<TcpIpPortAdministrationSnapshot>> GetTcpIpPortsAsync(
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult<IReadOnlyList<TcpIpPortAdministrationSnapshot>>(
                _ports.OrderBy(port => port.Address, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(port => port.PortNumber)
                    .ToArray());
        }

        public ValueTask<int> InsertTcpIpPortAsync(
            TcpIpPortAdministrationSnapshot port,
            CancellationToken cancellationToken)
        {
            InsertedPorts.Add(port);
            if (FailInsert)
            {
                throw new InvalidOperationException("Simulated TCP/IP port insert failure.");
            }

            return ValueTask.FromResult(30);
        }

        public ValueTask DeleteTcpIpPortByIdAsync(
            int databaseId,
            CancellationToken cancellationToken)
        {
            DeletedIds.Add(databaseId);
            if (FailDelete)
            {
                throw new InvalidOperationException("Simulated TCP/IP port delete failure.");
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask UpdateTcpIpPortAsync(
            TcpIpPortAdministrationSnapshot port,
            CancellationToken cancellationToken)
        {
            UpdatedPorts.Add(port);
            if (FailUpdate)
            {
                throw new InvalidOperationException("Simulated TCP/IP port update failure.");
            }

            return ValueTask.CompletedTask;
        }
    }
}
