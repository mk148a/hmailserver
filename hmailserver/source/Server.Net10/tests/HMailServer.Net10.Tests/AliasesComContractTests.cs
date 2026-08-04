using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class AliasesComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int DispEBadIndex = unchecked((int)0x8002000B);

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsDispatchIdsAndCompleteVtableOrder()
    {
        AssertContract(
            typeof(IInterfaceAliases),
            "11AA2C23-66BA-4DE0-92AB-C4F8DCC21D32",
            new[]
            {
                "get_Item", "get_Count", "Delete", "Refresh", "Add", "get_ItemByDBID",
                "DeleteByDBID", "get_ItemByName"
            });
        Assert.AreEqual(0, typeof(IInterfaceAliases).GetProperty("Item")?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(7, typeof(IInterfaceAliases).GetMethod("get_ItemByName")?.GetCustomAttribute<DispIdAttribute>()?.Value);

        AssertContract(
            typeof(IInterfaceAlias),
            "9420A3E9-ED5C-4699-98BE-0CBF3B7D3714",
            new[]
            {
                "get_Active", "set_Active", "get_DomainID", "set_DomainID", "get_ID",
                "get_Name", "set_Name", "get_Value", "set_Value", "Delete", "Save"
            });
        Assert.AreEqual(4, typeof(IInterfaceAlias).GetProperty(nameof(IInterfaceAlias.Name))?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(6, typeof(IInterfaceAlias).GetProperty(nameof(IInterfaceAlias.Value))?.GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<Aliases>(
            "1FE5E5F1-870A-4139-9EC1-DFFA3A9A58C8",
            "hMailServer.Aliases.1",
            typeof(IInterfaceAliases));
        AssertComClass<Alias>(
            "335CE9E1-32C5-4CB0-8BF6-CB925196E4D6",
            "hMailServer.Alias.1",
            typeof(IInterfaceAlias));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var aliasesError = Assert.ThrowsExactly<COMException>(() => _ = new Aliases().Count);
        var aliasesRefreshError = Assert.ThrowsExactly<COMException>(new Aliases().Refresh);
        var aliasError = Assert.ThrowsExactly<COMException>(() => _ = new Alias().Name);
        var aliasDomainIdSetterError = Assert.ThrowsExactly<COMException>(() => new Alias().DomainID = 42);

        Assert.AreEqual(EAccessDenied, aliasesError.ErrorCode);
        Assert.AreEqual(EAccessDenied, aliasesRefreshError.ErrorCode);
        Assert.AreEqual(EAccessDenied, aliasError.ErrorCode);
        Assert.AreEqual(EAccessDenied, aliasDomainIdSetterError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesOnlyReadOnlySnapshotAndLegacyLookupErrors()
    {
        IInterfaceAliases aliases = Aliases.CreateAuthorized(
            new[]
            {
                new AliasAdministrationSnapshot(10, 100, "abuse@example.test", "admin@example.test", true),
                new AliasAdministrationSnapshot(20, 100, "sales@example.test", "user@example.test", false)
            });

        Assert.AreEqual(2, aliases.Count);
        AssertAlias(aliases[0], 10, 100, "abuse@example.test", "admin@example.test", true);
        AssertAlias(aliases.get_ItemByName("SALES@EXAMPLE.TEST"), 20, 100, "sales@example.test", "user@example.test", false);
        AssertAlias(aliases.get_ItemByDBID(10), 10, 100, "abuse@example.test", "admin@example.test", true);

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = aliases[2]);
        var badName = Assert.ThrowsExactly<COMException>(() => _ = aliases.get_ItemByName("missing@example.test"));
        var pendingRefresh = Assert.ThrowsExactly<COMException>(aliases.Refresh);
        var pendingMutation = Assert.ThrowsExactly<COMException>(() => aliases[0].Value = "changed@example.test");

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badName.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRefresh.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMutation.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_RefreshAtomicallyReplacesSnapshotAndRetainsItOnFailure()
    {
        var failReload = false;
        var reloads = 0;
        IInterfaceAliases aliases = Aliases.CreateAuthorized(
            new[]
            {
                new AliasAdministrationSnapshot(10, 100, "abuse@example.test", "admin@example.test", true)
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
                    new AliasAdministrationSnapshot(20, 100, "billing@example.test", "billing-target@example.test", true),
                    new AliasAdministrationSnapshot(30, 100, "sales@example.test", "sales-target@example.test", false)
                };
            });

        Assert.AreEqual(1, aliases.Count);
        Assert.AreEqual("abuse@example.test", aliases[0].Name);

        aliases.Refresh();

        Assert.AreEqual(1, reloads);
        Assert.AreEqual(2, aliases.Count);
        AssertAlias(aliases[0], 20, 100, "billing@example.test", "billing-target@example.test", true);
        AssertAlias(
            aliases.get_ItemByName("SALES@EXAMPLE.TEST"),
            30,
            100,
            "sales@example.test",
            "sales-target@example.test",
            false);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = aliases.get_ItemByDBID(10)).ErrorCode);

        failReload = true;
        var refreshFailure = Assert.ThrowsExactly<COMException>(aliases.Refresh);

        Assert.AreEqual(EFail, refreshFailure.ErrorCode);
        Assert.AreEqual(2, reloads);
        Assert.AreEqual(2, aliases.Count);
        Assert.AreEqual("billing-target@example.test", aliases.get_ItemByDBID(20).Value);
    }

    [TestMethod]
    public void DomainAliases_UsesConfiguredRuntimeForSelectedDomain()
    {
        var store = new MutableAliasAdministrationStore(
            new[]
            {
                new AliasAdministrationSnapshot(10, 100, "abuse@example.test", "admin@example.test", true),
                new AliasAdministrationSnapshot(20, 200, "outside@example.test", "outside-target@example.test", true)
            });
        AliasAdministrationRuntimeHost.Configure(store);
        var domain = Domain.CreateAuthorized(new DomainAdministrationSnapshot(100, "example.test", true));

        var aliases = domain.Aliases;

        Assert.AreEqual(1, aliases.Count);
        Assert.AreEqual("abuse@example.test", aliases[0].Name);
        Assert.AreEqual(1, store.ReadCount);

        store.Replace(
            new[]
            {
                new AliasAdministrationSnapshot(30, 100, "billing@example.test", "billing@example.net", false),
                new AliasAdministrationSnapshot(40, 200, "outside@example.test", "outside-target@example.test", true)
            });

        aliases.Refresh();

        Assert.AreEqual(2, store.ReadCount);
        Assert.AreEqual(1, aliases.Count);
        AssertAlias(aliases[0], 30, 100, "billing@example.test", "billing@example.net", false);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = aliases.get_ItemByDBID(10)).ErrorCode);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = aliases.get_ItemByDBID(40)).ErrorCode);
    }

    [TestMethod]
    public void AuthorizedDomainAliases_AddStagesFieldsUsesOwningDomainAndPublishesAfterInsert()
    {
        var store = new MutableAliasAdministrationStore(
            new[]
            {
                new AliasAdministrationSnapshot(10, 100, "abuse@example.test", "admin@example.test", true)
            });
        AliasAdministrationRuntimeHost.Configure(store);
        var domain = Domain.CreateAuthorized(new DomainAdministrationSnapshot(100, "example.test", true));
        var aliases = domain.Aliases;

        var pending = aliases.Add();
        pending.DomainID = 999;
        pending.Name = "sales@example.test";
        pending.Value = "sales-target@example.test";
        pending.Active = true;

        Assert.AreEqual(0, pending.ID);
        Assert.AreEqual(100, pending.DomainID);
        pending.Save();

        Assert.AreEqual(30, pending.ID);
        Assert.AreEqual(2, aliases.Count);
        CollectionAssert.AreEqual(
            new[] { (OwningDomainId: 100, Alias: new AliasAdministrationSnapshot(0, 100, "sales@example.test", "sales-target@example.test", true)) },
            store.InsertedAliases);
        Assert.AreEqual(30, aliases.get_ItemByDBID(30).ID);
    }

    [TestMethod]
    public void AuthorizedDomainAliases_NewSaveFailureRetainsDraftAndCollectionForRetry()
    {
        var store = new MutableAliasAdministrationStore(
            new[]
            {
                new AliasAdministrationSnapshot(10, 100, "abuse@example.test", "admin@example.test", true)
            })
        {
            FailInsert = true
        };
        AliasAdministrationRuntimeHost.Configure(store);
        var domain = Domain.CreateAuthorized(new DomainAdministrationSnapshot(100, "example.test", true));
        var aliases = domain.Aliases;
        var pending = aliases.Add();
        pending.Name = "sales@example.test";
        pending.Value = "sales-target@example.test";

        var failure = Assert.ThrowsExactly<COMException>(pending.Save);

        Assert.AreEqual(EFail, failure.ErrorCode);
        Assert.AreEqual(0, pending.ID);
        Assert.AreEqual("sales@example.test", pending.Name);
        Assert.AreEqual(1, aliases.Count);

        store.FailInsert = false;
        pending.Save();

        Assert.AreEqual(30, pending.ID);
        Assert.AreEqual(2, aliases.Count);
    }

    [TestMethod]
    public void AuthorizedDomainAliases_RetainedNewItemRechecksAuthentication()
    {
        var isAuthenticated = true;
        var store = new MutableAliasAdministrationStore([]);
        AliasAdministrationRuntimeHost.Configure(store);
        var domain = Domain.CreateAuthorized(
            new DomainAdministrationSnapshot(100, "example.test", true),
            isAuthenticated: () => isAuthenticated);
        var pending = domain.Aliases.Add();
        pending.Name = "sales@example.test";
        isAuthenticated = false;

        var error = Assert.ThrowsExactly<COMException>(() => pending.Save());

        Assert.AreEqual(EAccessDenied, error.ErrorCode);
        Assert.AreEqual(0, store.InsertedAliases.Count);
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

    private static void AssertAlias(
        IInterfaceAlias alias,
        int id,
        int domainId,
        string name,
        string value,
        bool active)
    {
        Assert.AreEqual(id, alias.ID);
        Assert.AreEqual(domainId, alias.DomainID);
        Assert.AreEqual(name, alias.Name);
        Assert.AreEqual(value, alias.Value);
        Assert.AreEqual(active, alias.Active);
    }

    private sealed class MutableAliasAdministrationStore(IReadOnlyList<AliasAdministrationSnapshot> aliases)
        : IAliasAdministrationStore
    {
        private IReadOnlyList<AliasAdministrationSnapshot> _aliases = aliases;

        public int ReadCount { get; private set; }

        public bool FailInsert { get; set; }

        public List<(int OwningDomainId, AliasAdministrationSnapshot Alias)> InsertedAliases { get; } = [];

        public void Replace(IReadOnlyList<AliasAdministrationSnapshot> aliases)
        {
            _aliases = aliases;
        }

        public ValueTask<IReadOnlyList<AliasAdministrationSnapshot>> GetAliasesAsync(
            int domainId,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult<IReadOnlyList<AliasAdministrationSnapshot>>(
                _aliases.Where(alias => alias.DomainId == domainId).ToArray());
        }

        public ValueTask<int> InsertAliasAsync(
            int owningDomainId,
            AliasAdministrationSnapshot alias,
            CancellationToken cancellationToken)
        {
            InsertedAliases.Add((owningDomainId, alias));
            if (FailInsert)
            {
                throw new InvalidOperationException("Simulated alias insert failure.");
            }

            return ValueTask.FromResult(30);
        }
    }
}
