using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class DistributionListsComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int DispEBadIndex = unchecked((int)0x8002000B);

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsDispatchIdsAndCompleteVtableOrder()
    {
        AssertContract(
            typeof(IInterfaceDistributionLists),
            "8F0E22B8-0824-42DF-9260-F8B9ABFA8C61",
            new[]
            {
                "get_Item", "get_Count", "get_ItemByDBID", "Add", "DeleteByDBID",
                "get_ItemByAddress", "Refresh"
            });
        Assert.AreEqual(
            0,
            typeof(IInterfaceDistributionLists).GetProperty("Item")?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            5,
            typeof(IInterfaceDistributionLists).GetMethod("get_ItemByAddress")?.GetCustomAttribute<DispIdAttribute>()?.Value);

        AssertContract(
            typeof(IInterfaceDistributionList),
            "8251393D-27D8-4DF2-8A05-949C11D42C09",
            new[]
            {
                "get_ID", "Delete", "Save", "get_Active", "set_Active", "get_Recipients",
                "get_Address", "set_Address", "get_RequireSMTPAuth", "set_RequireSMTPAuth",
                "get_RequireSenderAddress", "set_RequireSenderAddress", "get_Mode", "set_Mode"
            });
        Assert.AreEqual(
            10,
            typeof(IInterfaceDistributionList).GetProperty(nameof(IInterfaceDistributionList.Mode))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<DistributionLists>(
            "C3DD0A4A-0551-442F-859A-76AAB92A6CF1",
            "hMailServer.DistributionLists.1",
            typeof(IInterfaceDistributionLists));
        AssertComClass<DistributionList>(
            "990D27ED-86CE-4DCB-B1C1-1E130C07F918",
            "hMailServer.DistributionList.1",
            typeof(IInterfaceDistributionList));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var listsError = Assert.ThrowsExactly<COMException>(() => _ = new DistributionLists().Count);
        var listsRefreshError = Assert.ThrowsExactly<COMException>(new DistributionLists().Refresh);
        var listError = Assert.ThrowsExactly<COMException>(() => _ = new DistributionList().Address);

        Assert.AreEqual(EAccessDenied, listsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, listsRefreshError.ErrorCode);
        Assert.AreEqual(EAccessDenied, listError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesOnlyReadOnlySnapshotAndLegacyLookupErrors()
    {
        IInterfaceDistributionLists lists = DistributionLists.CreateAuthorized(
            new[]
            {
                new DistributionListAdministrationSnapshot(
                    10,
                    100,
                    "announce@example.test",
                    true,
                    false,
                    string.Empty,
                    (int)ComDistributionListMode.Public),
                new DistributionListAdministrationSnapshot(
                    20,
                    100,
                    "members@example.test",
                    false,
                    true,
                    "owner@example.test",
                    (int)ComDistributionListMode.Membership)
            });

        Assert.AreEqual(2, lists.Count);
        AssertDistributionList(
            lists[0],
            10,
            "announce@example.test",
            true,
            false,
            string.Empty,
            ComDistributionListMode.Public);
        AssertDistributionList(
            lists.get_ItemByAddress("MEMBERS@EXAMPLE.TEST"),
            20,
            "members@example.test",
            false,
            true,
            "owner@example.test",
            ComDistributionListMode.Membership);
        AssertDistributionList(
            lists.get_ItemByDBID(10),
            10,
            "announce@example.test",
            true,
            false,
            string.Empty,
            ComDistributionListMode.Public);

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = lists[2]);
        var badAddress = Assert.ThrowsExactly<COMException>(() => _ = lists.get_ItemByAddress("missing@example.test"));
        var pendingRefresh = Assert.ThrowsExactly<COMException>(lists.Refresh);
        var pendingMutation = Assert.ThrowsExactly<COMException>(() => lists[0].Address = "changed@example.test");

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badAddress.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRefresh.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMutation.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_AddAndSave_StagesLegacyFieldsBindsOwnerAndAppendsAfterInsert()
    {
        var authenticated = true;
        DistributionListAdministrationSnapshot? inserted = null;
        var lists = DistributionLists.CreateAuthorized(
            Array.Empty<DistributionListAdministrationSnapshot>(),
            insert: draft =>
            {
                inserted = draft;
                return 42;
            },
            isAuthenticated: () => authenticated,
            domainId: 100);

        var pending = lists.Add();

        Assert.AreEqual(0, pending.ID);
        pending.Active = true;
        pending.Address = "announce@example.test";
        pending.RequireSMTPAuth = true;
        pending.RequireSenderAddress = "owner@example.test";
        pending.Mode = ComDistributionListMode.Membership;

        Assert.AreEqual(0, lists.Count);

        pending.Save();

        Assert.IsNotNull(inserted);
        Assert.AreEqual(0, inserted!.Id);
        Assert.AreEqual(100, inserted.DomainId);
        Assert.AreEqual("announce@example.test", inserted.Address);
        Assert.IsTrue(inserted.Active);
        Assert.IsTrue(inserted.RequireSmtpAuth);
        Assert.AreEqual("owner@example.test", inserted.RequireSenderAddress);
        Assert.AreEqual((int)ComDistributionListMode.Membership, inserted.Mode);
        Assert.AreEqual(42, pending.ID);
        Assert.AreEqual(1, lists.Count);
        Assert.AreEqual(42, lists[0].ID);
    }

    [TestMethod]
    public void AuthorizedCollection_FailedInsert_RetainsDraftAndDoesNotAppendOwnerSnapshot()
    {
        var insertAttempts = 0;
        var lists = DistributionLists.CreateAuthorized(
            Array.Empty<DistributionListAdministrationSnapshot>(),
            insert: draft =>
            {
                insertAttempts++;
                throw new InvalidOperationException("Simulated insert failure.");
            },
            isAuthenticated: () => true,
            domainId: 200);
        var pending = lists.Add();
        pending.Address = "draft@example.test";
        pending.RequireSenderAddress = "owner@example.test";

        var failure = Assert.ThrowsExactly<COMException>(pending.Save);

        Assert.AreEqual(EFail, failure.ErrorCode);
        Assert.AreEqual(1, insertAttempts);
        Assert.AreEqual(0, pending.ID);
        Assert.AreEqual("draft@example.test", pending.Address);
        Assert.AreEqual("owner@example.test", pending.RequireSenderAddress);
        Assert.AreEqual(0, lists.Count);
    }

    [TestMethod]
    public void AuthorizedCollection_ExistingSave_StagesAllFieldsAndReplacesOnlyMatchingOwnerSnapshot()
    {
        var updated = new List<DistributionListAdministrationSnapshot>();
        var lists = DistributionLists.CreateAuthorized(
            new[]
            {
                new DistributionListAdministrationSnapshot(
                    10,
                    100,
                    "announce@example.test",
                    true,
                    false,
                    string.Empty,
                    (int)ComDistributionListMode.Public),
                new DistributionListAdministrationSnapshot(
                    20,
                    100,
                    "members@example.test",
                    false,
                    true,
                    "owner@example.test",
                    (int)ComDistributionListMode.Membership)
            },
            update: snapshot =>
            {
                updated.Add(snapshot);
                return true;
            },
            isAuthenticated: () => true);

        var existing = lists.get_ItemByDBID(10);
        existing.Active = false;
        existing.Address = "updated@example.test";
        existing.RequireSMTPAuth = true;
        existing.RequireSenderAddress = "sender@example.test";
        existing.Mode = ComDistributionListMode.Announcement;
        existing.Save();

        Assert.AreEqual(1, updated.Count);
        Assert.AreEqual(10, updated[0].Id);
        Assert.AreEqual(100, updated[0].DomainId);
        Assert.AreEqual("updated@example.test", updated[0].Address);
        Assert.IsFalse(updated[0].Active);
        Assert.IsTrue(updated[0].RequireSmtpAuth);
        Assert.AreEqual("sender@example.test", updated[0].RequireSenderAddress);
        Assert.AreEqual((int)ComDistributionListMode.Announcement, updated[0].Mode);
        AssertDistributionList(
            lists.get_ItemByDBID(10),
            10,
            "updated@example.test",
            false,
            true,
            "sender@example.test",
            ComDistributionListMode.Announcement);
        AssertDistributionList(
            lists.get_ItemByDBID(20),
            20,
            "members@example.test",
            false,
            true,
            "owner@example.test",
            ComDistributionListMode.Membership);
    }

    [TestMethod]
    public void AuthorizedCollection_FailedExistingUpdate_RetainsStagedItemAndOwnerSnapshot()
    {
        var lists = DistributionLists.CreateAuthorized(
            new[]
            {
                new DistributionListAdministrationSnapshot(
                    10,
                    100,
                    "announce@example.test",
                    true,
                    false,
                    string.Empty,
                    (int)ComDistributionListMode.Public)
            },
            update: _ => false,
            isAuthenticated: () => true);
        var existing = lists.get_ItemByDBID(10);
        existing.Address = "staged@example.test";
        existing.Mode = ComDistributionListMode.Membership;

        var failure = Assert.ThrowsExactly<COMException>(existing.Save);

        Assert.AreEqual(EFail, failure.ErrorCode);
        Assert.AreEqual("staged@example.test", existing.Address);
        Assert.AreEqual(ComDistributionListMode.Membership, existing.Mode);
        AssertDistributionList(
            lists.get_ItemByDBID(10),
            10,
            "announce@example.test",
            true,
            false,
            string.Empty,
            ComDistributionListMode.Public);
    }

    [TestMethod]
    public void AuthorizedCollection_ExistingSave_RechecksLiveAuthentication()
    {
        var authenticated = true;
        var updateAttempts = 0;
        var lists = DistributionLists.CreateAuthorized(
            new[]
            {
                new DistributionListAdministrationSnapshot(
                    10,
                    100,
                    "announce@example.test",
                    true,
                    false,
                    string.Empty,
                    (int)ComDistributionListMode.Public)
            },
            update: _ =>
            {
                updateAttempts++;
                return true;
            },
            isAuthenticated: () => authenticated);
        var existing = lists.get_ItemByDBID(10);
        authenticated = false;

        Assert.AreEqual(
            EAccessDenied,
            Assert.ThrowsExactly<COMException>(() => existing.Address = "denied@example.test").ErrorCode);
        Assert.AreEqual(EAccessDenied, Assert.ThrowsExactly<COMException>(existing.Save).ErrorCode);
        Assert.AreEqual(0, updateAttempts);
        Assert.AreEqual("announce@example.test", lists.get_ItemByDBID(10).Address);
    }

    [TestMethod]
    public void AuthorizedCollection_LiveAuthenticationDeniesAddSettersAndSave()
    {
        var authenticated = true;
        var insertAttempts = 0;
        var lists = DistributionLists.CreateAuthorized(
            Array.Empty<DistributionListAdministrationSnapshot>(),
            insert: _ =>
            {
                insertAttempts++;
                return 7;
            },
            isAuthenticated: () => authenticated,
            domainId: 300);
        var pending = lists.Add();
        authenticated = false;

        Assert.AreEqual(EAccessDenied, Assert.ThrowsExactly<COMException>(lists.Add).ErrorCode);
        Assert.AreEqual(EAccessDenied, Assert.ThrowsExactly<COMException>(() => pending.Active = true).ErrorCode);
        Assert.AreEqual(EAccessDenied, Assert.ThrowsExactly<COMException>(() => pending.Address = "denied@example.test").ErrorCode);
        Assert.AreEqual(EAccessDenied, Assert.ThrowsExactly<COMException>(() => pending.RequireSMTPAuth = true).ErrorCode);
        Assert.AreEqual(EAccessDenied, Assert.ThrowsExactly<COMException>(() => pending.RequireSenderAddress = "denied@example.test").ErrorCode);
        Assert.AreEqual(EAccessDenied, Assert.ThrowsExactly<COMException>(() => pending.Mode = ComDistributionListMode.Membership).ErrorCode);
        Assert.AreEqual(EAccessDenied, Assert.ThrowsExactly<COMException>(pending.Save).ErrorCode);
        Assert.AreEqual(0, insertAttempts);
        Assert.AreEqual(0, lists.Count);
    }

    [TestMethod]
    public void AuthorizedCollection_WithoutAuthenticationCallback_RetainsNotImplementedMutationBoundary()
    {
        var lists = DistributionLists.CreateAuthorized(
            Array.Empty<DistributionListAdministrationSnapshot>(),
            insert: _ => 99);

        Assert.AreEqual(ENotImplemented, Assert.ThrowsExactly<COMException>(lists.Add).ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_RefreshAtomicallyReplacesSnapshotAndRetainsItOnFailure()
    {
        var failReload = false;
        var reloads = 0;
        IInterfaceDistributionLists lists = DistributionLists.CreateAuthorized(
            new[]
            {
                new DistributionListAdministrationSnapshot(
                    10,
                    100,
                    "announce@example.test",
                    true,
                    false,
                    string.Empty,
                    (int)ComDistributionListMode.Public)
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
                    new DistributionListAdministrationSnapshot(
                        20,
                        100,
                        "members@example.test",
                        false,
                        true,
                        "owner@example.test",
                        (int)ComDistributionListMode.Membership),
                    new DistributionListAdministrationSnapshot(
                        30,
                        100,
                        "readonly@example.test",
                        true,
                        false,
                        string.Empty,
                        (int)ComDistributionListMode.Announcement)
                };
            });

        Assert.AreEqual(1, lists.Count);
        Assert.AreEqual("announce@example.test", lists[0].Address);

        lists.Refresh();

        Assert.AreEqual(1, reloads);
        Assert.AreEqual(2, lists.Count);
        AssertDistributionList(
            lists[0],
            20,
            "members@example.test",
            false,
            true,
            "owner@example.test",
            ComDistributionListMode.Membership);
        Assert.AreEqual(ComDistributionListMode.Announcement, lists.get_ItemByDBID(30).Mode);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = lists.get_ItemByDBID(10)).ErrorCode);

        failReload = true;
        var refreshFailure = Assert.ThrowsExactly<COMException>(lists.Refresh);

        Assert.AreEqual(EFail, refreshFailure.ErrorCode);
        Assert.AreEqual(2, reloads);
        Assert.AreEqual(2, lists.Count);
        Assert.AreEqual("members@example.test", lists.get_ItemByDBID(20).Address);
    }

    [TestMethod]
    public void DomainDistributionLists_UsesConfiguredRuntimeForSelectedDomain()
    {
        var store = new MutableDistributionListAdministrationStore(
            new[]
            {
                new DistributionListAdministrationSnapshot(
                    10,
                    100,
                    "announce@example.test",
                    true,
                    false,
                    string.Empty,
                    (int)ComDistributionListMode.Public),
                new DistributionListAdministrationSnapshot(
                    20,
                    200,
                    "outside@example.test",
                    true,
                    false,
                    string.Empty,
                    (int)ComDistributionListMode.Public)
            });
        DistributionListAdministrationRuntimeHost.Configure(store);
        var domain = Domain.CreateAuthorized(new DomainAdministrationSnapshot(100, "example.test", true));

        var lists = domain.DistributionLists;

        Assert.AreEqual(1, lists.Count);
        Assert.AreEqual("announce@example.test", lists[0].Address);
        Assert.AreEqual(1, store.ReadCount);

        store.Replace(
            new[]
            {
                new DistributionListAdministrationSnapshot(
                    30,
                    100,
                    "members@example.test",
                    false,
                    true,
                    "owner@example.test",
                    (int)ComDistributionListMode.Membership),
                new DistributionListAdministrationSnapshot(
                    40,
                    200,
                    "outside-refreshed@example.test",
                    true,
                    false,
                    string.Empty,
                    (int)ComDistributionListMode.Public)
            });

        lists.Refresh();

        Assert.AreEqual(2, store.ReadCount);
        Assert.AreEqual(1, lists.Count);
        AssertDistributionList(
            lists[0],
            30,
            "members@example.test",
            false,
            true,
            "owner@example.test",
            ComDistributionListMode.Membership);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = lists.get_ItemByDBID(10)).ErrorCode);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = lists.get_ItemByDBID(40)).ErrorCode);
    }

    [TestMethod]
    public void DomainDistributionLists_PassesOwnerAndLiveAuthenticationToMutationFacade()
    {
        var authenticated = true;
        var store = new MutableDistributionListAdministrationStore(Array.Empty<DistributionListAdministrationSnapshot>());
        DistributionListAdministrationRuntimeHost.Configure(store);
        var domain = Domain.CreateAuthorized(
            new DomainAdministrationSnapshot(700, "example.test", true),
            () => authenticated);
        var lists = domain.DistributionLists;
        var pending = lists.Add();
        pending.Address = "announce@example.test";

        authenticated = false;
        Assert.AreEqual(EAccessDenied, Assert.ThrowsExactly<COMException>(pending.Save).ErrorCode);
        Assert.AreEqual(0, store.Inserted.Count);

        authenticated = true;
        pending.Save();

        Assert.AreEqual(1, store.Inserted.Count);
        Assert.AreEqual(700, store.Inserted[0].DomainId);
        Assert.AreEqual(1, lists.Count);
        Assert.AreEqual(pending.ID, lists[0].ID);
    }

    [TestMethod]
    public void DomainDistributionLists_ExistingSaveUsesOwnerScopedUpdate()
    {
        var store = new MutableDistributionListAdministrationStore(
            new[]
            {
                new DistributionListAdministrationSnapshot(
                    70,
                    700,
                    "announce@example.test",
                    true,
                    false,
                    string.Empty,
                    (int)ComDistributionListMode.Public),
                new DistributionListAdministrationSnapshot(
                    71,
                    701,
                    "outside@example.test",
                    true,
                    false,
                    string.Empty,
                    (int)ComDistributionListMode.Public)
            });
        DistributionListAdministrationRuntimeHost.Configure(store);
        var domain = Domain.CreateAuthorized(
            new DomainAdministrationSnapshot(700, "example.test", true),
            () => true);
        var lists = domain.DistributionLists;
        var existing = lists.get_ItemByDBID(70);

        existing.Address = "updated@example.test";
        existing.Save();

        Assert.AreEqual(1, store.Updated.Count);
        Assert.AreEqual(70, store.Updated[0].Id);
        Assert.AreEqual(700, store.Updated[0].DomainId);
        Assert.AreEqual("updated@example.test", lists.get_ItemByDBID(70).Address);
        Assert.AreEqual(1, lists.Count);
        Assert.AreEqual(0, store.Updated.Count(snapshot => snapshot.DomainId == 701));
    }

    [TestMethod]
    public void DomainDistributionLists_DeleteByDBIDAndAttachedDeleteRemoveOnlyOwnedSnapshotsAfterStoreSuccess()
    {
        var store = new MutableDistributionListAdministrationStore(
            new[]
            {
                new DistributionListAdministrationSnapshot(
                    70,
                    700,
                    "announce@example.test",
                    true,
                    false,
                    string.Empty,
                    (int)ComDistributionListMode.Public),
                new DistributionListAdministrationSnapshot(
                    71,
                    701,
                    "outside@example.test",
                    true,
                    false,
                    string.Empty,
                    (int)ComDistributionListMode.Public)
            });
        DistributionListAdministrationRuntimeHost.Configure(store);
        var domain = Domain.CreateAuthorized(
            new DomainAdministrationSnapshot(700, "example.test", true),
            () => true);
        var lists = domain.DistributionLists;
        var attached = lists.get_ItemByDBID(70);

        attached.Delete();

        Assert.AreEqual(1, store.Deleted.Count);
        Assert.AreEqual(700, store.Deleted[0].OwningDomainId);
        Assert.AreEqual(70, store.Deleted[0].DistributionListId);
        Assert.AreEqual(0, lists.Count);

        attached.Delete();
        Assert.AreEqual(1, store.Deleted.Count);

        lists.DeleteByDBID(71);
        Assert.AreEqual(1, store.Deleted.Count);

        var secondStore = new MutableDistributionListAdministrationStore(
            new[]
            {
                new DistributionListAdministrationSnapshot(
                    80,
                    700,
                    "members@example.test",
                    true,
                    false,
                    string.Empty,
                    (int)ComDistributionListMode.Public)
            });
        DistributionListAdministrationRuntimeHost.Configure(secondStore);
        var secondLists = Domain.CreateAuthorized(
            new DomainAdministrationSnapshot(700, "example.test", true),
            () => true).DistributionLists;

        secondLists.DeleteByDBID(80);

        Assert.AreEqual(700, secondStore.Deleted[0].OwningDomainId);
        Assert.AreEqual(80, secondStore.Deleted[0].DistributionListId);
        Assert.AreEqual(0, secondLists.Count);
    }

    [TestMethod]
    public void DomainDistributionLists_DeleteByDBID_IgnoresUnknownForeignAndStaleIds()
    {
        var store = new MutableDistributionListAdministrationStore(
            new[]
            {
                new DistributionListAdministrationSnapshot(
                    90,
                    900,
                    "announce@example.test",
                    true,
                    false,
                    string.Empty,
                    (int)ComDistributionListMode.Public),
                new DistributionListAdministrationSnapshot(
                    91,
                    901,
                    "outside@example.test",
                    true,
                    false,
                    string.Empty,
                    (int)ComDistributionListMode.Public)
            });
        DistributionListAdministrationRuntimeHost.Configure(store);
        var lists = Domain.CreateAuthorized(
            new DomainAdministrationSnapshot(900, "example.test", true),
            () => true).DistributionLists;

        lists.DeleteByDBID(999);
        lists.DeleteByDBID(91);
        lists.DeleteByDBID(90);
        lists.DeleteByDBID(90);

        Assert.AreEqual(1, store.Deleted.Count);
        Assert.AreEqual(90, store.Deleted[0].DistributionListId);
        Assert.AreEqual(0, lists.Count);
    }

    [TestMethod]
    public void DomainDistributionLists_FailedDeleteRetainsOwnerSnapshotAndMapsToFailure()
    {
        var store = new MutableDistributionListAdministrationStore(
            new[]
            {
                new DistributionListAdministrationSnapshot(
                    100,
                    1000,
                    "announce@example.test",
                    true,
                    false,
                    string.Empty,
                    (int)ComDistributionListMode.Public)
            })
        {
            DeleteSucceeds = false
        };
        DistributionListAdministrationRuntimeHost.Configure(store);
        var lists = Domain.CreateAuthorized(
            new DomainAdministrationSnapshot(1000, "example.test", true),
            () => true).DistributionLists;
        var attached = lists.get_ItemByDBID(100);

        var error = Assert.ThrowsExactly<COMException>(attached.Delete);

        Assert.AreEqual(EFail, error.ErrorCode);
        Assert.AreEqual(1, lists.Count);
        Assert.AreEqual(100, lists[0].ID);
        Assert.AreEqual(1, store.Deleted.Count);
    }

    [TestMethod]
    public void DomainDistributionLists_Delete_RechecksLiveAuthenticationAndRejectsZeroId()
    {
        var authenticated = true;
        var store = new MutableDistributionListAdministrationStore(Array.Empty<DistributionListAdministrationSnapshot>());
        DistributionListAdministrationRuntimeHost.Configure(store);
        var lists = Domain.CreateAuthorized(
            new DomainAdministrationSnapshot(1100, "example.test", true),
            () => authenticated).DistributionLists;
        var pending = lists.Add();

        var zeroIdError = Assert.ThrowsExactly<COMException>(pending.Delete);
        authenticated = false;
        var collectionError = Assert.ThrowsExactly<COMException>(() => lists.DeleteByDBID(999));
        var attachedError = Assert.ThrowsExactly<COMException>(pending.Delete);

        Assert.AreEqual(EFail, zeroIdError.ErrorCode);
        Assert.AreEqual(EAccessDenied, collectionError.ErrorCode);
        Assert.AreEqual(EAccessDenied, attachedError.ErrorCode);
        Assert.AreEqual(0, store.Deleted.Count);
        authenticated = true;
        Assert.AreEqual(0, lists.Count);
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

    private static void AssertDistributionList(
        IInterfaceDistributionList list,
        int id,
        string address,
        bool active,
        bool requireSmtpAuth,
        string requireSenderAddress,
        ComDistributionListMode mode)
    {
        Assert.AreEqual(id, list.ID);
        Assert.AreEqual(address, list.Address);
        Assert.AreEqual(active, list.Active);
        Assert.AreEqual(requireSmtpAuth, list.RequireSMTPAuth);
        Assert.AreEqual(requireSenderAddress, list.RequireSenderAddress);
        Assert.AreEqual(mode, list.Mode);
    }

    private sealed class MutableDistributionListAdministrationStore(
        IReadOnlyList<DistributionListAdministrationSnapshot> lists)
        : IDistributionListAdministrationStore
    {
        private IReadOnlyList<DistributionListAdministrationSnapshot> _lists = lists;

        public int ReadCount { get; private set; }

        public List<DistributionListAdministrationSnapshot> Inserted { get; } = new();

        public List<DistributionListAdministrationSnapshot> Updated { get; } = new();

        public List<(int OwningDomainId, int DistributionListId)> Deleted { get; } = new();

        public bool DeleteSucceeds { get; set; } = true;

        public void Replace(IReadOnlyList<DistributionListAdministrationSnapshot> lists)
        {
            _lists = lists;
        }

        public ValueTask<IReadOnlyList<DistributionListAdministrationSnapshot>> GetDistributionListsAsync(
            int domainId,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult<IReadOnlyList<DistributionListAdministrationSnapshot>>(
                _lists.Where(list => list.DomainId == domainId).ToArray());
        }

        public ValueTask<int> InsertDistributionListAsync(
            DistributionListAdministrationSnapshot distributionList,
            CancellationToken cancellationToken)
        {
            Inserted.Add(distributionList);
            return ValueTask.FromResult(900 + Inserted.Count);
        }

        public ValueTask<bool> UpdateDistributionListAsync(
            DistributionListAdministrationSnapshot distributionList,
            CancellationToken cancellationToken)
        {
            Updated.Add(distributionList);
            return ValueTask.FromResult(true);
        }

        public ValueTask<bool> DeleteDistributionListAsync(
            int owningDomainId,
            int distributionListId,
            CancellationToken cancellationToken)
        {
            Deleted.Add((owningDomainId, distributionListId));
            return ValueTask.FromResult(DeleteSucceeds);
        }
    }
}
