using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class DnsBlackListsComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int SFalse = 1;

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsDispatchIdsAndCompleteVtableOrder()
    {
        AssertContract(
            typeof(IInterfaceDNSBlackLists),
            "6B87D71F-93B7-4163-AA89-DA999A5A7239",
            new[]
            {
                "get_Item",
                "get_Count",
                "DeleteByDBID",
                "Add",
                "get_ItemByDBID",
                "Refresh",
                "get_ItemByDNSHost"
            });
        Assert.AreEqual(0, GetProperty(typeof(IInterfaceDNSBlackLists), "Item").GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(5, GetMethod(typeof(IInterfaceDNSBlackLists), "get_ItemByDBID").GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(7, GetMethod(typeof(IInterfaceDNSBlackLists), "get_ItemByDNSHost").GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            UnmanagedType.BStr,
            GetMethod(typeof(IInterfaceDNSBlackLists), "get_ItemByDNSHost")
                .GetParameters()[0]
                .GetCustomAttribute<MarshalAsAttribute>()?.Value);

        AssertContract(
            typeof(IInterfaceDNSBlackList),
            "6E011153-63D9-4B86-BA97-E55D152B221D",
            new[]
            {
                "get_Active",
                "set_Active",
                "get_ID",
                "get_DNSHost",
                "set_DNSHost",
                "get_RejectMessage",
                "set_RejectMessage",
                "get_ExpectedResult",
                "set_ExpectedResult",
                "Save",
                "get_Score",
                "set_Score",
                "Delete"
            });
        AssertVariantBoolProperty(typeof(IInterfaceDNSBlackList), nameof(IInterfaceDNSBlackList.Active), 1);
        AssertBstrProperty(typeof(IInterfaceDNSBlackList), nameof(IInterfaceDNSBlackList.DNSHost), 3);
        AssertBstrProperty(typeof(IInterfaceDNSBlackList), nameof(IInterfaceDNSBlackList.RejectMessage), 4);
        AssertBstrProperty(typeof(IInterfaceDNSBlackList), nameof(IInterfaceDNSBlackList.ExpectedResult), 5);
        Assert.AreEqual(7, GetProperty(typeof(IInterfaceDNSBlackList), nameof(IInterfaceDNSBlackList.Score)).GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<DNSBlackLists>(
            "39ECFFB4-B9EE-46C2-A84B-32D679FB3C82",
            "hMailServer.DNSBlackLists.1",
            typeof(IInterfaceDNSBlackLists));
        AssertComClass<DNSBlackList>(
            "E5907F7D-F13E-4D8A-A7DE-A29717C75A8F",
            "hMailServer.DNSBlackList.1",
            typeof(IInterfaceDNSBlackList));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var collectionError = Assert.ThrowsExactly<COMException>(() => _ = new DNSBlackLists().Count);
        var collectionRefreshError = Assert.ThrowsExactly<COMException>(new DNSBlackLists().Refresh);
        var itemError = Assert.ThrowsExactly<COMException>(() => _ = new DNSBlackList().DNSHost);
        var antiSpamError = Assert.ThrowsExactly<COMException>(() => _ = new AntiSpam().DNSBlackLists);

        Assert.AreEqual(EAccessDenied, collectionError.ErrorCode);
        Assert.AreEqual(EAccessDenied, collectionRefreshError.ErrorCode);
        Assert.AreEqual(EAccessDenied, itemError.ErrorCode);
        Assert.AreEqual(EAccessDenied, antiSpamError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesReadOnlySnapshotsAndLegacyLookupResults()
    {
        IInterfaceDNSBlackLists blackLists = DNSBlackLists.CreateAuthorized(
            new[]
            {
                Snapshot(10, true, "zen.spamhaus.org", "Rejected by Spamhaus.", "127.0.0.2-8", 4),
                Snapshot(20, false, "bl.spamcop.net", "Rejected by SpamCop.", "127.0.0.2", 3)
            });

        Assert.AreEqual(2, blackLists.Count);
        AssertBlackList(blackLists[0], 10, true, "zen.spamhaus.org", "Rejected by Spamhaus.", "127.0.0.2-8", 4);
        AssertBlackList(blackLists.get_ItemByDBID(20), 20, false, "bl.spamcop.net", "Rejected by SpamCop.", "127.0.0.2", 3);
        Assert.AreEqual(10, blackLists.get_ItemByDNSHost("ZEN.SPAMHAUS.ORG").ID);
        var missingHost = Assert.ThrowsExactly<COMException>(
            () => _ = blackLists.get_ItemByDNSHost("missing.example.test"));
        Assert.AreEqual(SFalse, missingHost.ErrorCode);

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = blackLists[2]);
        var badDatabaseId = Assert.ThrowsExactly<COMException>(() => _ = blackLists.get_ItemByDBID(30));
        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badDatabaseId.ErrorCode);

        AssertPending(() => blackLists.Add());
        AssertPending(() => blackLists.DeleteByDBID(10));
        AssertPending(blackLists.Refresh);
        AssertPending(() => blackLists[0].Active = false);
        AssertPending(() => blackLists[0].DNSHost = "changed.example.test");
        AssertPending(() => blackLists[0].RejectMessage = "Changed");
        AssertPending(() => blackLists[0].ExpectedResult = "127.0.0.9");
        AssertPending(() => blackLists[0].Score = 9);
        AssertPending(blackLists[0].Save);
        AssertPending(blackLists[0].Delete);
    }

    [TestMethod]
    public void AuthorizedDnsBlackLists_AddStagesFieldsAndPublishesOnlyAfterInsert()
    {
        DnsBlackListAdministrationSnapshot? inserted = null;
        IInterfaceDNSBlackLists blackLists = DNSBlackLists.CreateAuthorized(
            Array.Empty<DnsBlackListAdministrationSnapshot>(),
            insert: blackList =>
            {
                inserted = blackList;
                return 42;
            },
            isServerAdministrator: static () => true);
        var draft = blackLists.Add();

        draft.Active = true;
        draft.DNSHost = "zen.example.test";
        draft.RejectMessage = "Rejected";
        draft.ExpectedResult = "127.0.0.2";
        draft.Score = 4;

        Assert.AreEqual(0, draft.ID);
        Assert.AreEqual(0, blackLists.Count);

        draft.Save();

        Assert.IsNotNull(inserted);
        Assert.IsTrue(inserted!.Active);
        Assert.AreEqual("zen.example.test", inserted.DnsHost);
        Assert.AreEqual("Rejected", inserted.RejectMessage);
        Assert.AreEqual("127.0.0.2", inserted.ExpectedResult);
        Assert.AreEqual(4, inserted.Score);
        Assert.AreEqual(42, draft.ID);
        Assert.AreEqual(1, blackLists.Count);
        AssertBlackList(blackLists[0], 42, true, "zen.example.test", "Rejected", "127.0.0.2", 4);
    }

    [TestMethod]
    public void NewDnsBlackList_SaveFailureRetainsDraftAndOwnerSnapshot()
    {
        IInterfaceDNSBlackLists blackLists = DNSBlackLists.CreateAuthorized(
            Array.Empty<DnsBlackListAdministrationSnapshot>(),
            insert: static _ => throw new InvalidOperationException("Simulated insert failure."),
            isServerAdministrator: static () => true);
        var draft = blackLists.Add();
        draft.DNSHost = "failed.example.test";

        var error = Assert.ThrowsExactly<COMException>(draft.Save);

        Assert.AreEqual(EFail, error.ErrorCode);
        Assert.AreEqual(0, draft.ID);
        Assert.AreEqual("failed.example.test", draft.DNSHost);
        Assert.AreEqual(0, blackLists.Count);
    }

    [TestMethod]
    public void NewDnsBlackList_RechecksLiveAdministratorBeforeMutationAndSave()
    {
        var isAdministrator = true;
        IInterfaceDNSBlackLists blackLists = DNSBlackLists.CreateAuthorized(
            Array.Empty<DnsBlackListAdministrationSnapshot>(),
            insert: static _ => 42,
            isServerAdministrator: () => isAdministrator);
        var draft = blackLists.Add();

        isAdministrator = false;

        var setterError = Assert.ThrowsExactly<COMException>(() => draft.DNSHost = "denied.example.test");
        var saveError = Assert.ThrowsExactly<COMException>(draft.Save);

        Assert.AreEqual(EAccessDenied, setterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, saveError.ErrorCode);
        Assert.AreEqual(0, draft.ID);
        Assert.AreEqual(0, blackLists.Count);
    }

    [TestMethod]
    public void ExistingDnsBlackList_SaveUpdatesOnlyOwningSnapshotAfterSuccess()
    {
        DnsBlackListAdministrationSnapshot? updated = null;
        IInterfaceDNSBlackLists blackLists = DNSBlackLists.CreateAuthorized(
            new[] { Snapshot(10, false, "old.example.test", "Old", "127.0.0.2", 1) },
            update: blackList =>
            {
                updated = blackList;
                return true;
            },
            isServerAdministrator: static () => true);
        var item = blackLists[0];

        item.Active = true;
        item.DNSHost = "new.example.test";
        item.RejectMessage = "New";
        item.ExpectedResult = "127.0.0.9";
        item.Score = 7;
        item.Save();

        Assert.IsNotNull(updated);
        Assert.AreEqual(10, updated!.Id);
        Assert.IsTrue(updated.Active);
        Assert.AreEqual("new.example.test", updated.DnsHost);
        Assert.AreEqual("New", updated.RejectMessage);
        Assert.AreEqual("127.0.0.9", updated.ExpectedResult);
        Assert.AreEqual(7, updated.Score);
        Assert.AreEqual(1, blackLists.Count);
        AssertBlackList(blackLists[0], 10, true, "new.example.test", "New", "127.0.0.9", 7);
    }

    [TestMethod]
    public void ExistingDnsBlackList_SaveFailureRetainsStagedItemAndOwnerSnapshot()
    {
        IInterfaceDNSBlackLists blackLists = DNSBlackLists.CreateAuthorized(
            new[] { Snapshot(10, false, "old.example.test", "Old", "127.0.0.2", 1) },
            update: static _ => false,
            isServerAdministrator: static () => true);
        var item = blackLists[0];
        item.DNSHost = "staged.example.test";

        var error = Assert.ThrowsExactly<COMException>(item.Save);

        Assert.AreEqual(EFail, error.ErrorCode);
        Assert.AreEqual("staged.example.test", item.DNSHost);
        Assert.AreEqual("old.example.test", blackLists[0].DNSHost);
    }

    [TestMethod]
    public void ExistingDnsBlackList_RechecksLiveAdministratorBeforeMutationAndSave()
    {
        var isAdministrator = true;
        IInterfaceDNSBlackLists blackLists = DNSBlackLists.CreateAuthorized(
            new[] { Snapshot(10, false, "old.example.test", "Old", "127.0.0.2", 1) },
            update: static _ => true,
            isServerAdministrator: () => isAdministrator);
        var item = blackLists[0];

        isAdministrator = false;

        var setterError = Assert.ThrowsExactly<COMException>(() => item.DNSHost = "denied.example.test");
        var saveError = Assert.ThrowsExactly<COMException>(item.Save);

        Assert.AreEqual(EAccessDenied, setterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, saveError.ErrorCode);
        Assert.AreEqual("old.example.test", blackLists[0].DNSHost);
    }

    [TestMethod]
    public void DnsBlackLists_DeleteByDBIDRemovesOnlyTheSelectedSnapshotAfterSuccess()
    {
        var deletedId = 0;
        IInterfaceDNSBlackLists blackLists = DNSBlackLists.CreateAuthorized(
            new[]
            {
                Snapshot(10, true, "first.example.test", "First", "127.0.0.2", 1),
                Snapshot(20, false, "second.example.test", "Second", "127.0.0.9", 2)
            },
            delete: id =>
            {
                deletedId = id;
                return true;
            },
            isServerAdministrator: static () => true);

        blackLists.DeleteByDBID(10);

        Assert.AreEqual(10, deletedId);
        Assert.AreEqual(1, blackLists.Count);
        Assert.AreEqual(20, blackLists[0].ID);

        blackLists.DeleteByDBID(999);
        Assert.AreEqual(1, blackLists.Count);
    }

    [TestMethod]
    public void DnsBlackLists_DeleteFailureRetainsOwnerSnapshot()
    {
        IInterfaceDNSBlackLists blackLists = DNSBlackLists.CreateAuthorized(
            new[] { Snapshot(10, true, "first.example.test", "First", "127.0.0.2", 1) },
            delete: static _ => false,
            isServerAdministrator: static () => true);

        var error = Assert.ThrowsExactly<COMException>(() => blackLists.DeleteByDBID(10));

        Assert.AreEqual(EFail, error.ErrorCode);
        Assert.AreEqual(1, blackLists.Count);
        Assert.AreEqual(10, blackLists[0].ID);
    }

    [TestMethod]
    public void AttachedDnsBlackList_DeleteRechecksLiveAdministrator()
    {
        var isAdministrator = true;
        IInterfaceDNSBlackLists blackLists = DNSBlackLists.CreateAuthorized(
            new[] { Snapshot(10, true, "first.example.test", "First", "127.0.0.2", 1) },
            delete: static _ => true,
            isServerAdministrator: () => isAdministrator);
        var item = blackLists[0];

        isAdministrator = false;

        var error = Assert.ThrowsExactly<COMException>(item.Delete);

        Assert.AreEqual(EAccessDenied, error.ErrorCode);
        Assert.AreEqual(1, blackLists.Count);
    }

    [TestMethod]
    public void AuthorizedCollection_RefreshAtomicallyReplacesSnapshotAndRetainsItOnFailure()
    {
        var failReload = false;
        var reloads = 0;
        IInterfaceDNSBlackLists blackLists = DNSBlackLists.CreateAuthorized(
            new[]
            {
                Snapshot(10, true, "zen.spamhaus.org", "Rejected by Spamhaus.", "127.0.0.2-8", 4)
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
                    Snapshot(20, false, "bl.spamcop.net", "Rejected by SpamCop.", "127.0.0.2", 3),
                    Snapshot(30, true, "dnsbl.example.test", "Rejected by example.", "127.0.0.9", 5)
                };
            });

        Assert.AreEqual(1, blackLists.Count);
        Assert.AreEqual("zen.spamhaus.org", blackLists[0].DNSHost);

        blackLists.Refresh();

        Assert.AreEqual(1, reloads);
        Assert.AreEqual(2, blackLists.Count);
        AssertBlackList(blackLists[0], 20, false, "bl.spamcop.net", "Rejected by SpamCop.", "127.0.0.2", 3);
        Assert.AreEqual(30, blackLists.get_ItemByDNSHost("DNSBL.EXAMPLE.TEST").ID);
        var missingHost = Assert.ThrowsExactly<COMException>(
            () => _ = blackLists.get_ItemByDNSHost("zen.spamhaus.org"));
        Assert.AreEqual(SFalse, missingHost.ErrorCode);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = blackLists.get_ItemByDBID(10)).ErrorCode);

        failReload = true;
        var refreshFailure = Assert.ThrowsExactly<COMException>(blackLists.Refresh);

        Assert.AreEqual(EFail, refreshFailure.ErrorCode);
        Assert.AreEqual(2, reloads);
        Assert.AreEqual(2, blackLists.Count);
        Assert.AreEqual("Rejected by example.", blackLists.get_ItemByDBID(30).RejectMessage);
    }

    [TestMethod]
    public void AuthorizedAntiSpam_UsesConfiguredDnsBlackListRuntime()
    {
        var store = new MutableDnsBlackListAdministrationStore(
            new[]
            {
                Snapshot(20, false, "bl.spamcop.net", "Rejected by SpamCop.", "127.0.0.2", 3),
                Snapshot(10, true, "zen.spamhaus.org", "Rejected by Spamhaus.", "127.0.0.2-8", 4)
            });
        DnsBlackListAdministrationRuntimeHost.Configure(
            store);
        var antiSpam = AntiSpam.CreateAuthorized(new AntiSpamAdministrationSnapshot());

        var blackLists = antiSpam.DNSBlackLists;

        Assert.AreEqual(2, blackLists.Count);
        Assert.AreEqual(10, blackLists[0].ID);
        Assert.AreEqual("Rejected by SpamCop.", blackLists.get_ItemByDBID(20).RejectMessage);
        Assert.AreEqual(1, store.ReadCount);

        store.Replace(
            new[]
            {
                Snapshot(20, false, "bl.spamcop.net", "Rejected by SpamCop.", "127.0.0.2", 3),
                Snapshot(30, true, "dnsbl.example.test", "Rejected by example.", "127.0.0.9", 5)
            });

        blackLists.Refresh();

        Assert.AreEqual(2, store.ReadCount);
        Assert.AreEqual(2, blackLists.Count);
        Assert.AreEqual(20, blackLists[0].ID);
        Assert.AreEqual(30, blackLists.get_ItemByDNSHost("dnsbl.example.test").ID);
        var missingHost = Assert.ThrowsExactly<COMException>(
            () => _ = blackLists.get_ItemByDNSHost("zen.spamhaus.org"));
        Assert.AreEqual(SFalse, missingHost.ErrorCode);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = blackLists.get_ItemByDBID(10)).ErrorCode);
    }

    private static DnsBlackListAdministrationSnapshot Snapshot(
        int id,
        bool active,
        string dnsHost,
        string rejectMessage,
        string expectedResult,
        int score) =>
        new(id, active, dnsHost, rejectMessage, expectedResult, score);

    private static void AssertBlackList(
        IInterfaceDNSBlackList blackList,
        int id,
        bool active,
        string dnsHost,
        string rejectMessage,
        string expectedResult,
        int score)
    {
        Assert.AreEqual(id, blackList.ID);
        Assert.AreEqual(active, blackList.Active);
        Assert.AreEqual(dnsHost, blackList.DNSHost);
        Assert.AreEqual(rejectMessage, blackList.RejectMessage);
        Assert.AreEqual(expectedResult, blackList.ExpectedResult);
        Assert.AreEqual(score, blackList.Score);
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

    private static void AssertVariantBoolProperty(Type contract, string name, int dispatchId)
    {
        var property = GetProperty(contract, name);

        Assert.AreEqual(dispatchId, property.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(UnmanagedType.VariantBool, property.GetMethod?.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(UnmanagedType.VariantBool, property.SetMethod?.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>()?.Value);
    }

    private static void AssertBstrProperty(Type contract, string name, int dispatchId)
    {
        var property = GetProperty(contract, name);

        Assert.AreEqual(dispatchId, property.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(UnmanagedType.BStr, property.GetMethod?.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(UnmanagedType.BStr, property.SetMethod?.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>()?.Value);
    }

    private static PropertyInfo GetProperty(Type contract, string name) =>
        contract.GetProperty(name) ?? throw new AssertFailedException($"Missing property {name}.");

    private static MethodInfo GetMethod(Type contract, string name) =>
        contract.GetMethod(name) ?? throw new AssertFailedException($"Missing method {name}.");

    private static void AssertPending(Action action)
    {
        var error = Assert.ThrowsExactly<COMException>(action);
        Assert.AreEqual(ENotImplemented, error.ErrorCode);
    }

    private sealed class MutableDnsBlackListAdministrationStore(
        IReadOnlyList<DnsBlackListAdministrationSnapshot> blackLists)
        : IDnsBlackListAdministrationStore
    {
        private IReadOnlyList<DnsBlackListAdministrationSnapshot> _blackLists = blackLists;

        public int ReadCount { get; private set; }

        public void Replace(IReadOnlyList<DnsBlackListAdministrationSnapshot> blackLists)
        {
            _blackLists = blackLists;
        }

        public ValueTask<IReadOnlyList<DnsBlackListAdministrationSnapshot>> GetDnsBlackListsAsync(
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult<IReadOnlyList<DnsBlackListAdministrationSnapshot>>(
                _blackLists.OrderBy(static blackList => blackList.Id).ToArray());
        }
    }
}
