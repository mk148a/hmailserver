using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class DomainAliasesComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int DispEBadIndex = unchecked((int)0x8002000B);

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsDispatchIdsAndCompleteVtableOrder()
    {
        AssertContract(
            typeof(IInterfaceDomainAliases),
            "E4100C8D-E956-449C-A96D-261DDC33AE4F",
            new[]
            {
                "get_Item", "get_Count", "get_ItemByDBID", "Refresh", "Delete",
                "DeleteByDBID", "Add"
            });
        Assert.AreEqual(
            0,
            typeof(IInterfaceDomainAliases).GetProperty("Item")?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            6,
            typeof(IInterfaceDomainAliases).GetMethod("Add")?.GetCustomAttribute<DispIdAttribute>()?.Value);

        AssertContract(
            typeof(IInterfaceDomainAlias),
            "8FD251D8-AAF1-4143-B185-E6C1BF281826",
            new[]
            {
                "get_ID", "get_AliasName", "set_AliasName", "get_DomainID", "set_DomainID",
                "Save", "Delete"
            });
        Assert.AreEqual(
            2,
            typeof(IInterfaceDomainAlias).GetProperty(nameof(IInterfaceDomainAlias.AliasName))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<DomainAliases>(
            "DC25B3AD-0360-49CA-AD4B-06FA42B9DF04",
            "hMailServer.DomainAliases.1",
            typeof(IInterfaceDomainAliases));
        AssertComClass<DomainAlias>(
            "D0061C74-5588-4796-B564-FE5DE85495DC",
            "hMailServer.DomainAlias.1",
            typeof(IInterfaceDomainAlias));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var aliasesError = Assert.ThrowsExactly<COMException>(() => _ = new DomainAliases().Count);
        var aliasesRefreshError = Assert.ThrowsExactly<COMException>(new DomainAliases().Refresh);
        var aliasError = Assert.ThrowsExactly<COMException>(() => _ = new DomainAlias().AliasName);

        Assert.AreEqual(EAccessDenied, aliasesError.ErrorCode);
        Assert.AreEqual(EAccessDenied, aliasesRefreshError.ErrorCode);
        Assert.AreEqual(EAccessDenied, aliasError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesOnlyReadOnlySnapshotAndLegacyLookupErrors()
    {
        IInterfaceDomainAliases aliases = DomainAliases.CreateAuthorized(
            new[]
            {
                new DomainAliasAdministrationSnapshot(10, 100, "alias-one.test"),
                new DomainAliasAdministrationSnapshot(20, 100, "alias-two.test")
            });

        Assert.AreEqual(2, aliases.Count);
        AssertDomainAlias(aliases[0], 10, 100, "alias-one.test");
        AssertDomainAlias(aliases.get_ItemByDBID(20), 20, 100, "alias-two.test");

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = aliases[2]);
        var badDatabaseId = Assert.ThrowsExactly<COMException>(() => _ = aliases.get_ItemByDBID(30));
        var pendingRefresh = Assert.ThrowsExactly<COMException>(aliases.Refresh);
        var pendingDelete = Assert.ThrowsExactly<COMException>(() => aliases.Delete(0));
        var pendingMutation = Assert.ThrowsExactly<COMException>(() => aliases[0].AliasName = "changed.test");

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badDatabaseId.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRefresh.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDelete.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMutation.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_RefreshAtomicallyReplacesSnapshotAndRetainsItOnFailure()
    {
        var failReload = false;
        var reloads = 0;
        IInterfaceDomainAliases aliases = DomainAliases.CreateAuthorized(
            new[]
            {
                new DomainAliasAdministrationSnapshot(10, 100, "alias-one.test")
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
                    new DomainAliasAdministrationSnapshot(20, 100, "alias-two.test"),
                    new DomainAliasAdministrationSnapshot(30, 100, "alias-three.test")
                };
            });

        Assert.AreEqual(1, aliases.Count);
        Assert.AreEqual("alias-one.test", aliases[0].AliasName);

        aliases.Refresh();

        Assert.AreEqual(1, reloads);
        Assert.AreEqual(2, aliases.Count);
        AssertDomainAlias(aliases[0], 20, 100, "alias-two.test");
        AssertDomainAlias(aliases.get_ItemByDBID(30), 30, 100, "alias-three.test");
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = aliases.get_ItemByDBID(10)).ErrorCode);

        failReload = true;
        var refreshFailure = Assert.ThrowsExactly<COMException>(aliases.Refresh);

        Assert.AreEqual(EFail, refreshFailure.ErrorCode);
        Assert.AreEqual(2, reloads);
        Assert.AreEqual(2, aliases.Count);
        Assert.AreEqual("alias-two.test", aliases.get_ItemByDBID(20).AliasName);
    }

    [TestMethod]
    public void DomainAliases_UsesConfiguredRuntimeForSelectedDomain()
    {
        var store = new MutableDomainAliasAdministrationStore(
            new[]
            {
                new DomainAliasAdministrationSnapshot(10, 100, "alias-one.test"),
                new DomainAliasAdministrationSnapshot(20, 200, "outside.test")
            });
        DomainAliasAdministrationRuntimeHost.Configure(store);
        var domain = Domain.CreateAuthorized(new DomainAdministrationSnapshot(100, "example.test", true));

        var aliases = domain.DomainAliases;

        Assert.AreEqual(1, aliases.Count);
        Assert.AreEqual("alias-one.test", aliases[0].AliasName);
        Assert.AreEqual(1, store.ReadCount);

        store.Replace(
            new[]
            {
                new DomainAliasAdministrationSnapshot(30, 100, "alias-refreshed.test"),
                new DomainAliasAdministrationSnapshot(40, 200, "outside-refreshed.test")
            });

        aliases.Refresh();

        Assert.AreEqual(2, store.ReadCount);
        Assert.AreEqual(1, aliases.Count);
        AssertDomainAlias(aliases[0], 30, 100, "alias-refreshed.test");
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = aliases.get_ItemByDBID(10)).ErrorCode);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = aliases.get_ItemByDBID(40)).ErrorCode);
    }

    [TestMethod]
    public void AuthorizedDomainAliases_AddStagesOwningDomainAndPublishesAfterInsert()
    {
        var store = new MutableDomainAliasAdministrationStore(
            new[]
            {
                new DomainAliasAdministrationSnapshot(10, 100, "alias-one.test"),
                new DomainAliasAdministrationSnapshot(20, 200, "outside.test")
            });
        DomainAliasAdministrationRuntimeHost.Configure(store);
        var aliases = Domain.CreateAuthorized(new DomainAdministrationSnapshot(100, "example.test", true)).DomainAliases;
        var pending = aliases.Add();

        pending.DomainID = 999;
        pending.AliasName = "alias-new.test";
        pending.Save();

        CollectionAssert.AreEqual(
            new[]
            {
                (OwningDomainId: 100, Alias: new DomainAliasAdministrationSnapshot(0, 100, "alias-new.test"))
            },
            store.InsertedAliases);
        Assert.AreEqual(30, pending.ID);
        Assert.AreEqual(2, aliases.Count);
        Assert.AreEqual("alias-new.test", aliases.get_ItemByDBID(30).AliasName);
    }

    [TestMethod]
    public void AuthorizedDomainAliases_NewSaveFailureRetainsDraftAndAllowsRetry()
    {
        var store = new MutableDomainAliasAdministrationStore(Array.Empty<DomainAliasAdministrationSnapshot>())
        {
            FailInsert = true
        };
        DomainAliasAdministrationRuntimeHost.Configure(store);
        var aliases = Domain.CreateAuthorized(new DomainAdministrationSnapshot(100, "example.test", true)).DomainAliases;
        var pending = aliases.Add();
        pending.AliasName = "alias-retry.test";

        var error = Assert.ThrowsExactly<COMException>(pending.Save);

        Assert.AreEqual(EFail, error.ErrorCode);
        Assert.AreEqual(0, pending.ID);
        Assert.AreEqual("alias-retry.test", pending.AliasName);
        Assert.AreEqual(0, aliases.Count);

        store.FailInsert = false;
        pending.Save();

        Assert.AreEqual(30, pending.ID);
        Assert.AreEqual(1, aliases.Count);
    }

    [TestMethod]
    public void AuthorizedDomainAliases_RetainedNewItemRechecksAuthentication()
    {
        var isAuthenticated = true;
        var store = new MutableDomainAliasAdministrationStore(Array.Empty<DomainAliasAdministrationSnapshot>());
        DomainAliasAdministrationRuntimeHost.Configure(store);
        var aliases = Domain.CreateAuthorized(
            new DomainAdministrationSnapshot(100, "example.test", true),
            isAuthenticated: () => isAuthenticated).DomainAliases;
        var pending = aliases.Add();
        pending.AliasName = "alias-auth.test";

        isAuthenticated = false;
        var error = Assert.ThrowsExactly<COMException>(pending.Save);

        Assert.AreEqual(EAccessDenied, error.ErrorCode);
        Assert.AreEqual(0, store.InsertedAliases.Count);
        isAuthenticated = true;
        Assert.AreEqual(0, aliases.Count);
    }

    [TestMethod]
    public void AuthorizedDomainAliases_ExistingSaveUsesOwnerAndPublishesAfterUpdate()
    {
        var store = new MutableDomainAliasAdministrationStore(
            new[]
            {
                new DomainAliasAdministrationSnapshot(10, 100, "alias-one.test"),
                new DomainAliasAdministrationSnapshot(20, 200, "outside.test")
            });
        DomainAliasAdministrationRuntimeHost.Configure(store);
        var aliases = Domain.CreateAuthorized(new DomainAdministrationSnapshot(100, "example.test", true)).DomainAliases;
        var existing = aliases[0];

        existing.DomainID = 999;
        existing.AliasName = "alias-updated.test";
        existing.Save();

        CollectionAssert.AreEqual(
            new[]
            {
                (OwningDomainId: 100, Alias: new DomainAliasAdministrationSnapshot(10, 100, "alias-updated.test"))
            },
            store.UpdatedAliases);
        Assert.AreEqual("alias-updated.test", aliases.get_ItemByDBID(10).AliasName);
    }

    [TestMethod]
    public void AuthorizedDomainAliases_ExistingSaveFailureRetainsSnapshotAndAllowsRetry()
    {
        var store = new MutableDomainAliasAdministrationStore(
            new[] { new DomainAliasAdministrationSnapshot(10, 100, "alias-one.test") })
        {
            FailUpdate = true
        };
        DomainAliasAdministrationRuntimeHost.Configure(store);
        var aliases = Domain.CreateAuthorized(new DomainAdministrationSnapshot(100, "example.test", true)).DomainAliases;
        var existing = aliases[0];
        existing.AliasName = "alias-retry.test";

        var error = Assert.ThrowsExactly<COMException>(existing.Save);

        Assert.AreEqual(EFail, error.ErrorCode);
        Assert.AreEqual("alias-retry.test", existing.AliasName);
        Assert.AreEqual("alias-one.test", aliases.get_ItemByDBID(10).AliasName);

        store.FailUpdate = false;
        existing.Save();

        Assert.AreEqual("alias-retry.test", aliases.get_ItemByDBID(10).AliasName);
    }

    [TestMethod]
    public void AuthorizedDomainAliases_ExistingSaveRechecksAuthenticationAndNoOpsWhenStale()
    {
        var isAuthenticated = true;
        var store = new MutableDomainAliasAdministrationStore(
            new[] { new DomainAliasAdministrationSnapshot(10, 100, "alias-one.test") });
        DomainAliasAdministrationRuntimeHost.Configure(store);
        var aliases = Domain.CreateAuthorized(
            new DomainAdministrationSnapshot(100, "example.test", true),
            isAuthenticated: () => isAuthenticated).DomainAliases;
        var existing = aliases[0];
        existing.AliasName = "alias-auth.test";

        isAuthenticated = false;
        var authError = Assert.ThrowsExactly<COMException>(existing.Save);
        Assert.AreEqual(EAccessDenied, authError.ErrorCode);
        Assert.AreEqual(0, store.UpdatedAliases.Count);

        isAuthenticated = true;
        store.Replace(Array.Empty<DomainAliasAdministrationSnapshot>());
        aliases.Refresh();
        existing.Save();

        Assert.AreEqual(0, store.UpdatedAliases.Count);
        Assert.AreEqual(0, aliases.Count);
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

    private static void AssertDomainAlias(
        IInterfaceDomainAlias alias,
        int id,
        int domainId,
        string aliasName)
    {
        Assert.AreEqual(id, alias.ID);
        Assert.AreEqual(domainId, alias.DomainID);
        Assert.AreEqual(aliasName, alias.AliasName);
    }

    private sealed class MutableDomainAliasAdministrationStore(IReadOnlyList<DomainAliasAdministrationSnapshot> aliases)
        : IDomainAliasAdministrationStore
    {
        private IReadOnlyList<DomainAliasAdministrationSnapshot> _aliases = aliases;

        public int ReadCount { get; private set; }

        public bool FailInsert { get; set; }

        public bool FailUpdate { get; set; }

        public List<(int OwningDomainId, DomainAliasAdministrationSnapshot Alias)> InsertedAliases { get; } = [];

        public List<(int OwningDomainId, DomainAliasAdministrationSnapshot Alias)> UpdatedAliases { get; } = [];

        public void Replace(IReadOnlyList<DomainAliasAdministrationSnapshot> aliases)
        {
            _aliases = aliases;
        }

        public ValueTask<IReadOnlyList<DomainAliasAdministrationSnapshot>> GetDomainAliasesAsync(
            int domainId,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult<IReadOnlyList<DomainAliasAdministrationSnapshot>>(
                _aliases.Where(alias => alias.DomainId == domainId).ToArray());
        }

        public ValueTask<int> InsertDomainAliasAsync(
            int owningDomainId,
            DomainAliasAdministrationSnapshot alias,
            CancellationToken cancellationToken)
        {
            InsertedAliases.Add((owningDomainId, alias));
            if (FailInsert)
            {
                throw new InvalidOperationException("Simulated domain alias insert failure.");
            }

            return ValueTask.FromResult(30);
        }

        public ValueTask UpdateDomainAliasAsync(
            int owningDomainId,
            DomainAliasAdministrationSnapshot alias,
            CancellationToken cancellationToken)
        {
            UpdatedAliases.Add((owningDomainId, alias));
            if (FailUpdate)
            {
                throw new InvalidOperationException("Simulated domain alias update failure.");
            }

            return ValueTask.CompletedTask;
        }
    }
}
