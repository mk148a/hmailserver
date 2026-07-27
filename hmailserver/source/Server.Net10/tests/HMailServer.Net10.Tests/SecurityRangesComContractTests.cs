using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SecurityRangesComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int DispEBadIndex = unchecked((int)0x8002000B);

    private const int AllowSmtp = 1;
    private const int AllowPop3 = 2;
    private const int AllowImap = 8;
    private const int RelayLocalToLocal = 64;
    private const int RelayLocalToRemote = 128;
    private const int RelayRemoteToLocal = 256;
    private const int RelayRemoteToRemote = 512;
    private const int SpamProtection = 1024;
    private const int VirusProtection = 4096;
    private const int SmtpAuthLocalToLocal = 8192;
    private const int SmtpAuthLocalToExternal = 16384;
    private const int SmtpAuthExternalToLocal = 32768;
    private const int SmtpAuthExternalToExternal = 65536;
    private const int RequireTlsForAuth = 131072;

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsDispatchIdsAndCompleteVtableOrder()
    {
        AssertContract(
            typeof(IInterfaceSecurityRanges),
            "3F0053E1-2328-452F-855D-87FF63E06BE0",
            new[]
            {
                "get_Item", "get_ItemByDBID", "Delete", "DeleteByDBID",
                "Refresh", "Add", "get_Count", "get_ItemByName", "SetDefault"
            });
        Assert.AreEqual(
            0,
            typeof(IInterfaceSecurityRanges).GetProperty("Item")?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            8,
            typeof(IInterfaceSecurityRanges).GetMethod(nameof(IInterfaceSecurityRanges.SetDefault))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);

        AssertContract(
            typeof(IInterfaceSecurityRange),
            "3B1CB89D-9248-413D-BF2A-F000E6DB5F54",
            new[]
            {
                "get_ID",
                "get_LowerIP", "set_LowerIP",
                "get_UpperIP", "set_UpperIP",
                "get_AllowSMTPConnections", "set_AllowSMTPConnections",
                "get_AllowPOP3Connections", "set_AllowPOP3Connections",
                "get_Priority", "set_Priority",
                "Save",
                "get_AllowIMAPConnections", "set_AllowIMAPConnections",
                "get_Name", "set_Name",
                "get_RequireAuthForDeliveryToLocal", "set_RequireAuthForDeliveryToLocal",
                "get_RequireAuthForDeliveryToRemote", "set_RequireAuthForDeliveryToRemote",
                "get_AllowDeliveryFromLocalToLocal", "set_AllowDeliveryFromLocalToLocal",
                "get_AllowDeliveryFromLocalToRemote", "set_AllowDeliveryFromLocalToRemote",
                "get_AllowDeliveryFromRemoteToLocal", "set_AllowDeliveryFromRemoteToLocal",
                "get_AllowDeliveryFromRemoteToRemote", "set_AllowDeliveryFromRemoteToRemote",
                "get_EnableSpamProtection", "set_EnableSpamProtection",
                "get_IsForwardingRelay", "set_IsForwardingRelay",
                "get_EnableAntiVirus", "set_EnableAntiVirus",
                "Delete",
                "get_Expires", "set_Expires",
                "get_ExpiresTime", "set_ExpiresTime",
                "get_RequireSMTPAuthLocalToLocal", "set_RequireSMTPAuthLocalToLocal",
                "get_RequireSMTPAuthLocalToExternal", "set_RequireSMTPAuthLocalToExternal",
                "get_RequireSMTPAuthExternalToLocal", "set_RequireSMTPAuthExternalToLocal",
                "get_RequireSMTPAuthExternalToExternal", "set_RequireSMTPAuthExternalToExternal",
                "get_RequireSSLTLSForAuth", "set_RequireSSLTLSForAuth"
            });
        Assert.AreEqual(
            29,
            typeof(IInterfaceSecurityRange).GetProperty(nameof(IInterfaceSecurityRange.RequireSSLTLSForAuth))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<SecurityRanges>(
            "60A752A2-1197-4841-ADD4-CE922873E794",
            "hMailServer.SecurityRanges.1",
            typeof(IInterfaceSecurityRanges));
        AssertComClass<SecurityRange>(
            "B149383D-151C-4585-99F8-71876D0F14C4",
            "hMailServer.SecurityRange.1",
            typeof(IInterfaceSecurityRange));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var rangesError = Assert.ThrowsExactly<COMException>(() => _ = new SecurityRanges().Count);
        var rangesRefreshError = Assert.ThrowsExactly<COMException>(new SecurityRanges().Refresh);
        var rangesIndexDeleteError = Assert.ThrowsExactly<COMException>(() => new SecurityRanges().Delete(0));
        var rangesDeleteByIdError = Assert.ThrowsExactly<COMException>(() => new SecurityRanges().DeleteByDBID(10));
        var rangeError = Assert.ThrowsExactly<COMException>(() => _ = new SecurityRange().Priority);
        var rangeDeleteError = Assert.ThrowsExactly<COMException>(() => new SecurityRange().Delete());
        var settingsError = Assert.ThrowsExactly<COMException>(() => _ = new Settings().SecurityRanges);

        Assert.AreEqual(EAccessDenied, rangesError.ErrorCode);
        Assert.AreEqual(EAccessDenied, rangesRefreshError.ErrorCode);
        Assert.AreEqual(EAccessDenied, rangesIndexDeleteError.ErrorCode);
        Assert.AreEqual(EAccessDenied, rangesDeleteByIdError.ErrorCode);
        Assert.AreEqual(EAccessDenied, rangeError.ErrorCode);
        Assert.AreEqual(EAccessDenied, rangeDeleteError.ErrorCode);
        Assert.AreEqual(EAccessDenied, settingsError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesReadOnlySnapshotsAndLegacyLookupErrors()
    {
        var expiresTime = new DateTime(2026, 7, 1, 2, 3, 4);
        IInterfaceSecurityRanges ranges = SecurityRanges.CreateAuthorized(
            new[]
            {
                Snapshot(
                    id: 10,
                    name: "Internet",
                    lowerIp: "0.0.0.0",
                    upperIp: "255.255.255.255",
                    priority: 10,
                    options: AllOptions,
                    expires: true,
                    expiresTime: expiresTime),
                Snapshot(
                    id: 20,
                    name: "My computer",
                    lowerIp: "127.0.0.1",
                    upperIp: "127.0.0.1",
                    priority: 30,
                    options: AllowSmtp,
                    expires: false,
                    expiresTime: new DateTime(2001, 1, 1))
            });

        Assert.AreEqual(2, ranges.Count);
        AssertRange(ranges[0], 10, "Internet", "0.0.0.0", "255.255.255.255", 10, expiresTime);
        Assert.AreEqual(20, ranges.get_ItemByDBID(20).ID);
        Assert.AreEqual(20, ranges.get_ItemByName("MY COMPUTER").ID);
        Assert.IsFalse(ranges[1].AllowPOP3Connections);

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = ranges[2]);
        var badDatabaseId = Assert.ThrowsExactly<COMException>(() => _ = ranges.get_ItemByDBID(30));
        var badName = Assert.ThrowsExactly<COMException>(() => _ = ranges.get_ItemByName("missing"));
        var pendingDelete = Assert.ThrowsExactly<COMException>(() => ranges.Delete(0));
        var pendingDeleteById = Assert.ThrowsExactly<COMException>(() => ranges.DeleteByDBID(10));
        var pendingRefresh = Assert.ThrowsExactly<COMException>(ranges.Refresh);
        var pendingAdd = Assert.ThrowsExactly<COMException>(() => ranges.Add());
        var pendingSetDefault = Assert.ThrowsExactly<COMException>(ranges.SetDefault);
        var pendingMutation = Assert.ThrowsExactly<COMException>(() => ranges[0].LowerIP = "10.0.0.1");
        var pendingSave = Assert.ThrowsExactly<COMException>(ranges[0].Save);
        var pendingRangeDelete = Assert.ThrowsExactly<COMException>(ranges[0].Delete);

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badDatabaseId.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badName.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDelete.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDeleteById.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRefresh.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAdd.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingSetDefault.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingSave.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRangeDelete.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_AddStagesNewRangeAndAppendsOnlyAfterInsert()
    {
        SecurityRangeAdministrationSnapshot? inserted = null;
        IInterfaceSecurityRanges ranges = SecurityRanges.CreateAuthorized(
            new[]
            {
                Snapshot(
                    id: 10,
                    name: "Existing",
                    lowerIp: "127.0.0.1",
                    upperIp: "127.0.0.1",
                    priority: 20,
                    options: AllowSmtp,
                    expires: false,
                    expiresTime: new DateTime(2001, 1, 1))
            },
            insert: range =>
            {
                inserted = range;
                return 30;
            },
            isServerAdministrator: static () => true);

        var pending = ranges.Add();

        Assert.AreEqual(0, pending.ID);
        Assert.AreEqual("0.0.0.0", pending.LowerIP);
        Assert.AreEqual("0.0.0.0", pending.UpperIP);
        Assert.AreEqual(0, pending.Priority);
        Assert.IsFalse(pending.Expires);
        Assert.AreEqual(new DateTime(2001, 1, 1), pending.ExpiresTime);

        pending.Name = "LAN";
        pending.LowerIP = "192.168.1.1";
        pending.LowerIP = "not-an-ip";
        pending.UpperIP = "192.168.1.254";
        pending.UpperIP = "300.300.300.300";
        pending.Priority = 40;
        pending.AllowSMTPConnections = true;
        pending.AllowPOP3Connections = true;
        pending.AllowIMAPConnections = true;
        pending.AllowDeliveryFromLocalToLocal = true;
        pending.AllowDeliveryFromLocalToRemote = true;
        pending.AllowDeliveryFromRemoteToLocal = true;
        pending.AllowDeliveryFromRemoteToRemote = true;
        pending.EnableSpamProtection = true;
        pending.EnableAntiVirus = true;
        pending.RequireSMTPAuthLocalToLocal = true;
        pending.RequireSMTPAuthLocalToExternal = true;
        pending.RequireSMTPAuthExternalToLocal = true;
        pending.RequireSMTPAuthExternalToExternal = true;
        pending.RequireSSLTLSForAuth = true;
        pending.Expires = true;
        pending.ExpiresTime = new DateTime(2026, 7, 27, 1, 2, 3);
        pending.RequireAuthForDeliveryToLocal = true;
        pending.RequireAuthForDeliveryToRemote = true;
        pending.IsForwardingRelay = true;

        Assert.AreEqual("192.168.1.1", pending.LowerIP);
        Assert.AreEqual("192.168.1.254", pending.UpperIP);

        pending.Save();

        Assert.IsNotNull(inserted);
        Assert.AreEqual(0, inserted!.Id);
        Assert.AreEqual("LAN", inserted.Name);
        Assert.AreEqual("192.168.1.1", inserted.LowerIp);
        Assert.AreEqual("192.168.1.254", inserted.UpperIp);
        Assert.AreEqual(40, inserted.Priority);
        Assert.AreEqual(AllOptions, inserted.Options);
        Assert.IsTrue(inserted.Expires);
        Assert.AreEqual(new DateTime(2026, 7, 27, 1, 2, 3), inserted.ExpiresTime);

        Assert.AreEqual(30, pending.ID);
        Assert.AreEqual(2, ranges.Count);
        Assert.AreEqual(10, ranges[0].ID);
        Assert.AreEqual(30, ranges[1].ID);
        Assert.AreEqual("LAN", ranges.get_ItemByDBID(30).Name);
    }

    [TestMethod]
    public void AuthorizedSettings_SecurityRangeDeleteUsesOwningCollectionForAllItemLookups()
    {
        var store = new MutableSecurityRangeAdministrationStore(
            new[]
            {
                Snapshot(10, "Internet", "0.0.0.0", "255.255.255.255", 10, AllOptions, false, new DateTime(2001, 1, 1)),
                Snapshot(20, "LAN", "192.168.1.1", "192.168.1.254", 20, AllOptions, false, new DateTime(2001, 1, 1)),
                Snapshot(30, "Loopback", "127.0.0.1", "127.0.0.1", 30, AllOptions, false, new DateTime(2001, 1, 1))
            });
        SecurityRangeAdministrationRuntimeHost.Configure(store);
        IInterfaceSettings settings = Settings.CreateAuthorized(isServerAdministrator: static () => true);
        var ranges = settings.SecurityRanges;

        ranges[0].Delete();
        ranges.get_ItemByDBID(20).Delete();
        ranges.get_ItemByName("internet").Delete();

        CollectionAssert.AreEqual(new[] { 30, 20, 10 }, store.DeletedIds);
        Assert.AreEqual(0, ranges.Count);
    }

    [TestMethod]
    public void AuthorizedSettings_SecurityRangeDeleteMapsFailureAndRetainsOwningSnapshot()
    {
        var store = new MutableSecurityRangeAdministrationStore(
            new[]
            {
                Snapshot(10, "Internet", "0.0.0.0", "255.255.255.255", 10, AllOptions, false, new DateTime(2001, 1, 1)),
                Snapshot(20, "LAN", "192.168.1.1", "192.168.1.254", 20, AllOptions, false, new DateTime(2001, 1, 1))
            })
        {
            FailDelete = true
        };
        SecurityRangeAdministrationRuntimeHost.Configure(store);
        IInterfaceSettings settings = Settings.CreateAuthorized(isServerAdministrator: static () => true);
        var ranges = settings.SecurityRanges;

        var error = Assert.ThrowsExactly<COMException>(() => ranges.get_ItemByDBID(10).Delete());

        Assert.AreEqual(EFail, error.ErrorCode);
        CollectionAssert.AreEqual(new[] { 10 }, store.DeletedIds);
        Assert.AreEqual(2, ranges.Count);
        Assert.AreEqual("Internet", ranges.get_ItemByDBID(10).Name);
    }

    [TestMethod]
    public void AuthorizedSettings_SecurityRangeDeleteIsNoOpForRepeatedStaleItem()
    {
        var store = new MutableSecurityRangeAdministrationStore(
            new[]
            {
                Snapshot(10, "Internet", "0.0.0.0", "255.255.255.255", 10, AllOptions, false, new DateTime(2001, 1, 1)),
                Snapshot(20, "LAN", "192.168.1.1", "192.168.1.254", 20, AllOptions, false, new DateTime(2001, 1, 1))
            });
        SecurityRangeAdministrationRuntimeHost.Configure(store);
        IInterfaceSettings settings = Settings.CreateAuthorized(isServerAdministrator: static () => true);
        var ranges = settings.SecurityRanges;
        var item = ranges.get_ItemByDBID(10);

        item.Delete();
        item.Delete();

        CollectionAssert.AreEqual(new[] { 10 }, store.DeletedIds);
        Assert.AreEqual(1, ranges.Count);
        Assert.AreEqual(20, ranges[0].ID);
    }

    [TestMethod]
    public void AuthorizedSettings_SecurityRangeDeleteOnUnsavedAddIsNoOp()
    {
        var store = new MutableSecurityRangeAdministrationStore(
            new[]
            {
                Snapshot(10, "Internet", "0.0.0.0", "255.255.255.255", 10, AllOptions, false, new DateTime(2001, 1, 1))
            });
        SecurityRangeAdministrationRuntimeHost.Configure(store);
        IInterfaceSettings settings = Settings.CreateAuthorized(isServerAdministrator: static () => true);
        var ranges = settings.SecurityRanges;
        var item = ranges.Add();

        Assert.AreEqual(0, item.ID);
        item.Delete();

        CollectionAssert.AreEqual(Array.Empty<int>(), store.DeletedIds);
        Assert.AreEqual(1, ranges.Count);
        Assert.AreEqual(10, ranges[0].ID);
    }

    [TestMethod]
    public void AuthorizedSettings_SecurityRangeDeleteRechecksServerAdministrator()
    {
        var isServerAdministrator = true;
        var store = new MutableSecurityRangeAdministrationStore(
            new[]
            {
                Snapshot(10, "Internet", "0.0.0.0", "255.255.255.255", 10, AllOptions, false, new DateTime(2001, 1, 1))
            });
        SecurityRangeAdministrationRuntimeHost.Configure(store);
        IInterfaceSettings settings = Settings.CreateAuthorized(isServerAdministrator: () => isServerAdministrator);
        var ranges = settings.SecurityRanges;
        var item = ranges[0];
        isServerAdministrator = false;

        var error = Assert.ThrowsExactly<COMException>(() => item.Delete());

        Assert.AreEqual(EAccessDenied, error.ErrorCode);
        CollectionAssert.AreEqual(Array.Empty<int>(), store.DeletedIds);
        Assert.AreEqual(1, ranges.Count);
    }

    [TestMethod]
    public void AuthorizedSettings_SecurityRangesDeleteByDBIDScopesStoreCallAndNoOpsForUnknownOrRepeatedIds()
    {
        var store = new MutableSecurityRangeAdministrationStore(
            new[]
            {
                Snapshot(10, "Internet", "0.0.0.0", "255.255.255.255", 10, AllOptions, false, new DateTime(2001, 1, 1)),
                Snapshot(20, "LAN", "192.168.1.1", "192.168.1.254", 20, AllOptions, false, new DateTime(2001, 1, 1))
            });
        SecurityRangeAdministrationRuntimeHost.Configure(store);
        IInterfaceSettings settings = Settings.CreateAuthorized(isServerAdministrator: static () => true);
        var ranges = settings.SecurityRanges;

        ranges.DeleteByDBID(30);
        ranges.DeleteByDBID(20);
        ranges.DeleteByDBID(20);

        CollectionAssert.AreEqual(new[] { 20 }, store.DeletedIds);
        Assert.AreEqual(1, ranges.Count);
        Assert.AreEqual(10, ranges[0].ID);
    }

    [TestMethod]
    public void AuthorizedSettings_SecurityRangesDeleteByIndexUsesOwningSnapshotAndNoOpsInvalidIndexes()
    {
        var store = new MutableSecurityRangeAdministrationStore(
            new[]
            {
                Snapshot(10, "Internet", "0.0.0.0", "255.255.255.255", 10, AllOptions, false, new DateTime(2001, 1, 1)),
                Snapshot(20, "LAN", "192.168.1.1", "192.168.1.254", 20, AllOptions, false, new DateTime(2001, 1, 1)),
                Snapshot(30, "Loopback", "127.0.0.1", "127.0.0.1", 30, AllOptions, false, new DateTime(2001, 1, 1))
            });
        SecurityRangeAdministrationRuntimeHost.Configure(store);
        IInterfaceSettings settings = Settings.CreateAuthorized(isServerAdministrator: static () => true);
        var ranges = settings.SecurityRanges;

        ranges.Delete(1);
        ranges.Delete(-1);
        ranges.Delete(2);
        ranges.Delete(1);
        ranges.Delete(0);
        ranges.Delete(0);

        CollectionAssert.AreEqual(new[] { 20, 10, 30 }, store.DeletedIds);
        Assert.AreEqual(0, ranges.Count);
    }

    [TestMethod]
    public void AuthorizedSettings_SecurityRangesDeleteByIndexMapsFailureAndRetainsSnapshot()
    {
        var store = new MutableSecurityRangeAdministrationStore(
            new[]
            {
                Snapshot(10, "Internet", "0.0.0.0", "255.255.255.255", 10, AllOptions, false, new DateTime(2001, 1, 1)),
                Snapshot(20, "LAN", "192.168.1.1", "192.168.1.254", 20, AllOptions, false, new DateTime(2001, 1, 1))
            })
        {
            FailDelete = true
        };
        SecurityRangeAdministrationRuntimeHost.Configure(store);
        IInterfaceSettings settings = Settings.CreateAuthorized(isServerAdministrator: static () => true);
        var ranges = settings.SecurityRanges;

        var error = Assert.ThrowsExactly<COMException>(() => ranges.Delete(0));

        Assert.AreEqual(EFail, error.ErrorCode);
        CollectionAssert.AreEqual(new[] { 20 }, store.DeletedIds);
        Assert.AreEqual(2, ranges.Count);
        Assert.AreEqual(20, ranges[0].ID);
        Assert.AreEqual(10, ranges[1].ID);
    }

    [TestMethod]
    public void AuthorizedSettings_SecurityRangesDeleteByDBIDMapsFailureAndRetainsSnapshot()
    {
        var store = new MutableSecurityRangeAdministrationStore(
            new[]
            {
                Snapshot(10, "Internet", "0.0.0.0", "255.255.255.255", 10, AllOptions, false, new DateTime(2001, 1, 1)),
                Snapshot(20, "LAN", "192.168.1.1", "192.168.1.254", 20, AllOptions, false, new DateTime(2001, 1, 1))
            })
        {
            FailDelete = true
        };
        SecurityRangeAdministrationRuntimeHost.Configure(store);
        IInterfaceSettings settings = Settings.CreateAuthorized(isServerAdministrator: static () => true);
        var ranges = settings.SecurityRanges;

        var error = Assert.ThrowsExactly<COMException>(() => ranges.DeleteByDBID(10));

        Assert.AreEqual(EFail, error.ErrorCode);
        CollectionAssert.AreEqual(new[] { 10 }, store.DeletedIds);
        Assert.AreEqual(2, ranges.Count);
        Assert.AreEqual("Internet", ranges.get_ItemByDBID(10).Name);
        Assert.AreEqual("LAN", ranges.get_ItemByDBID(20).Name);
    }

    [TestMethod]
    public void AuthorizedSettings_SecurityRangesRequiresServerAdministrator()
    {
        SecurityRangeAdministrationRuntimeHost.Configure(
            new MutableSecurityRangeAdministrationStore(Array.Empty<SecurityRangeAdministrationSnapshot>()));
        IInterfaceSettings settings = Settings.CreateAuthorized(isServerAdministrator: static () => false);

        var error = Assert.ThrowsExactly<COMException>(() => _ = settings.SecurityRanges);

        Assert.AreEqual(EAccessDenied, error.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_RefreshAtomicallyReplacesSnapshotAndRetainsItOnFailure()
    {
        var failReload = false;
        var reloads = 0;
        var expiresTime = new DateTime(2026, 7, 1, 2, 3, 4);
        var refreshedExpiresTime = new DateTime(2026, 8, 2, 3, 4, 5);
        IInterfaceSecurityRanges ranges = SecurityRanges.CreateAuthorized(
            new[]
            {
                Snapshot(
                    id: 10,
                    name: "Internet",
                    lowerIp: "0.0.0.0",
                    upperIp: "255.255.255.255",
                    priority: 10,
                    options: AllOptions,
                    expires: true,
                    expiresTime: expiresTime)
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
                    Snapshot(
                        id: 20,
                        name: "My computer",
                        lowerIp: "127.0.0.1",
                        upperIp: "127.0.0.1",
                        priority: 30,
                        options: AllOptions,
                        expires: true,
                        expiresTime: refreshedExpiresTime),
                    Snapshot(
                        id: 30,
                        name: "LAN",
                        lowerIp: "192.168.1.1",
                        upperIp: "192.168.1.254",
                        priority: 20,
                        options: AllOptions,
                        expires: true,
                        expiresTime: refreshedExpiresTime)
                };
            });

        Assert.AreEqual(1, ranges.Count);
        Assert.AreEqual("Internet", ranges[0].Name);

        ranges.Refresh();

        Assert.AreEqual(1, reloads);
        Assert.AreEqual(2, ranges.Count);
        AssertRange(ranges[0], 20, "My computer", "127.0.0.1", "127.0.0.1", 30, refreshedExpiresTime);
        Assert.AreEqual(30, ranges.get_ItemByName("LAN").ID);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = ranges.get_ItemByDBID(10)).ErrorCode);

        failReload = true;
        var refreshFailure = Assert.ThrowsExactly<COMException>(ranges.Refresh);

        Assert.AreEqual(EFail, refreshFailure.ErrorCode);
        Assert.AreEqual(2, reloads);
        Assert.AreEqual(2, ranges.Count);
        Assert.AreEqual("My computer", ranges.get_ItemByDBID(20).Name);
    }

    [TestMethod]
    public void AuthorizedSettings_UsesConfiguredSecurityRangeRuntime()
    {
        var store = new MutableSecurityRangeAdministrationStore(
            new[]
            {
                Snapshot(20, "My computer", "127.0.0.1", "127.0.0.1", 30, AllowSmtp, false, new DateTime(2001, 1, 1)),
                Snapshot(10, "Internet", "0.0.0.0", "255.255.255.255", 10, AllOptions, false, new DateTime(2001, 1, 1))
            });
        SecurityRangeAdministrationRuntimeHost.Configure(store);
        IInterfaceSettings settings = Settings.CreateAuthorized();

        var ranges = settings.SecurityRanges;

        Assert.AreEqual(2, ranges.Count);
        Assert.AreEqual("My computer", ranges[0].Name);
        Assert.AreEqual(30, ranges[0].Priority);
        Assert.AreEqual(1, store.ReadCount);

        store.Replace(
            new[]
            {
                Snapshot(30, "LAN", "192.168.1.1", "192.168.1.254", 40, AllOptions, false, new DateTime(2001, 1, 1)),
                Snapshot(10, "Internet", "0.0.0.0", "255.255.255.255", 10, AllOptions, false, new DateTime(2001, 1, 1))
            });

        ranges.Refresh();

        Assert.AreEqual(2, store.ReadCount);
        Assert.AreEqual(2, ranges.Count);
        Assert.AreEqual("LAN", ranges[0].Name);
        Assert.AreEqual(40, ranges[0].Priority);
        Assert.AreEqual(30, ranges.get_ItemByDBID(30).ID);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = ranges.get_ItemByDBID(20)).ErrorCode);
    }

    private static int AllOptions =>
        AllowSmtp
        | AllowPop3
        | AllowImap
        | RelayLocalToLocal
        | RelayLocalToRemote
        | RelayRemoteToLocal
        | RelayRemoteToRemote
        | SpamProtection
        | VirusProtection
        | SmtpAuthLocalToLocal
        | SmtpAuthLocalToExternal
        | SmtpAuthExternalToLocal
        | SmtpAuthExternalToExternal
        | RequireTlsForAuth;

    private static SecurityRangeAdministrationSnapshot Snapshot(
        int id,
        string name,
        string lowerIp,
        string upperIp,
        int priority,
        int options,
        bool expires,
        DateTime expiresTime) =>
        new(id, name, lowerIp, upperIp, priority, options, expires, expiresTime);

    private static void AssertRange(
        IInterfaceSecurityRange range,
        int id,
        string name,
        string lowerIp,
        string upperIp,
        int priority,
        DateTime expiresTime)
    {
        Assert.AreEqual(id, range.ID);
        Assert.AreEqual(name, range.Name);
        Assert.AreEqual(lowerIp, range.LowerIP);
        Assert.AreEqual(upperIp, range.UpperIP);
        Assert.AreEqual(priority, range.Priority);
        Assert.IsTrue(range.AllowSMTPConnections);
        Assert.IsTrue(range.AllowPOP3Connections);
        Assert.IsTrue(range.AllowIMAPConnections);
        Assert.IsTrue(range.AllowDeliveryFromLocalToLocal);
        Assert.IsTrue(range.AllowDeliveryFromLocalToRemote);
        Assert.IsTrue(range.AllowDeliveryFromRemoteToLocal);
        Assert.IsTrue(range.AllowDeliveryFromRemoteToRemote);
        Assert.IsTrue(range.EnableSpamProtection);
        Assert.IsTrue(range.EnableAntiVirus);
        Assert.IsTrue(range.RequireSMTPAuthLocalToLocal);
        Assert.IsTrue(range.RequireSMTPAuthLocalToExternal);
        Assert.IsTrue(range.RequireSMTPAuthExternalToLocal);
        Assert.IsTrue(range.RequireSMTPAuthExternalToExternal);
        Assert.IsTrue(range.RequireSSLTLSForAuth);
        Assert.IsTrue(range.Expires);
        Assert.AreEqual(expiresTime, range.ExpiresTime);
        Assert.IsFalse(range.RequireAuthForDeliveryToLocal);
        Assert.IsFalse(range.RequireAuthForDeliveryToRemote);
        Assert.IsFalse(range.IsForwardingRelay);
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

    private sealed class MutableSecurityRangeAdministrationStore(IReadOnlyList<SecurityRangeAdministrationSnapshot> ranges)
        : ISecurityRangeAdministrationStore
    {
        private IReadOnlyList<SecurityRangeAdministrationSnapshot> _ranges = ranges;

        public int ReadCount { get; private set; }

        public bool FailDelete { get; set; }

        public List<int> DeletedIds { get; } = [];

        public void Replace(IReadOnlyList<SecurityRangeAdministrationSnapshot> ranges)
        {
            _ranges = ranges;
        }

        public ValueTask<IReadOnlyList<SecurityRangeAdministrationSnapshot>> GetSecurityRangesAsync(
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult<IReadOnlyList<SecurityRangeAdministrationSnapshot>>(
                _ranges.OrderBy(static range => range.Expires)
                    .ThenByDescending(static range => range.Priority)
                    .ThenBy(static range => range.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray());
        }

        public ValueTask<int> InsertSecurityRangeAsync(
            SecurityRangeAdministrationSnapshot range,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(100);

        public ValueTask DeleteSecurityRangeByIdAsync(
            int databaseId,
            CancellationToken cancellationToken)
        {
            DeletedIds.Add(databaseId);
            if (FailDelete)
            {
                throw new InvalidOperationException("Simulated delete failure.");
            }

            return ValueTask.CompletedTask;
        }
    }
}
