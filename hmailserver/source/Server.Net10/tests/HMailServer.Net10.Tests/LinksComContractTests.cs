using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class LinksComContractTests
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);

    [TestMethod]
    public void Interface_PreservesLegacyIidDispatchIdsAndCompleteVtableOrder()
    {
        var contract = typeof(IInterfaceLinks);

        Assert.AreEqual(new Guid("E252D063-7E86-4FCE-B702-A5E89E0DFB48"), contract.GUID);
        Assert.AreEqual(
            ComInterfaceType.InterfaceIsDual,
            contract.GetCustomAttribute<InterfaceTypeAttribute>()?.Value);
        Assert.AreEqual(
            TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable,
            contract.GetCustomAttribute<TypeLibTypeAttribute>()?.Value);

        var methods = contract
            .GetMethods()
            .OrderBy(static method => method.MetadataToken)
            .ToArray();
        CollectionAssert.AreEqual(
            new[] { "get_Domain", "get_Account", "get_Alias", "get_DistributionList" },
            methods.Select(static method => method.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { 1, 2, 3, 4 },
            methods
                .Select(static method => method.GetCustomAttribute<DispIdAttribute>()?.Value ?? -1)
                .ToArray());
    }

    [TestMethod]
    public void ComClass_PreservesLegacyIdentityAndDefaultInterface()
    {
        var type = typeof(Links);

        Assert.AreEqual(new Guid("88A65C5B-916D-4A79-948A-B0DEE0454804"), type.GUID);
        Assert.AreEqual("hMailServer.Links.1", type.GetCustomAttribute<ProgIdAttribute>()?.Value);
        Assert.AreEqual(ClassInterfaceType.None, type.GetCustomAttribute<ClassInterfaceAttribute>()?.Value);
        Assert.AreEqual(typeof(IInterfaceLinks), type.GetCustomAttribute<ComDefaultInterfaceAttribute>()?.Value);
        Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        IInterfaceLinks links = new Links();

        AssertAccessDenied(() => _ = links.get_Domain(10));
        AssertAccessDenied(() => _ = links.get_Account(20));
        AssertAccessDenied(() => _ = links.get_Alias(30));
        AssertAccessDenied(() => _ = links.get_DistributionList(40));
    }

    [TestMethod]
    public void AuthorizedAdapter_ResolvesLegacyObjectsThroughExistingAdministrationStores()
    {
        var domains = new RecordingDomainStore(
            new[]
            {
                new DomainAdministrationSnapshot(10, "alpha.example", true),
                new DomainAdministrationSnapshot(11, "beta.example", true)
            });
        var accounts = new RecordingAccountStore(
            new AccountAdministrationSnapshot(20, 11, "user@beta.example", true, AdminLevel: 0));
        var aliases = new RecordingAliasStore(
            new AliasAdministrationSnapshot(30, 10, "sales@alpha.example", "user@alpha.example", true));
        var lists = new RecordingDistributionListStore(
            new DistributionListAdministrationSnapshot(
                40,
                11,
                "team@beta.example",
                true,
                RequireSmtpAuth: false,
                RequireSenderAddress: string.Empty,
                Mode: 0));
        IInterfaceLinks links = Links.CreateAuthorized(domains, accounts, aliases, lists);

        Assert.AreEqual("alpha.example", links.get_Domain(10).Name);
        Assert.AreEqual("user@beta.example", links.get_Account(20).Address);
        Assert.AreEqual("sales@alpha.example", links.get_Alias(30).Name);
        Assert.AreEqual("team@beta.example", links.get_DistributionList(40).Address);

        CollectionAssert.AreEqual(new[] { 10, 11 }, accounts.DomainIds.ToArray());
        CollectionAssert.AreEqual(new[] { 10 }, aliases.DomainIds.ToArray());
        CollectionAssert.AreEqual(new[] { 10, 11 }, lists.DomainIds.ToArray());
    }

    [TestMethod]
    public void AuthorizedAdapter_ReturnsLegacyBadIndexForUnknownDatabaseIdentifiers()
    {
        var domains = new RecordingDomainStore(
            new[] { new DomainAdministrationSnapshot(10, "alpha.example", true) });
        IInterfaceLinks links = Links.CreateAuthorized(
            domains,
            new RecordingAccountStore(),
            new RecordingAliasStore(),
            new RecordingDistributionListStore());

        AssertBadIndex(() => _ = links.get_Domain(999));
        AssertBadIndex(() => _ = links.get_Account(999));
        AssertBadIndex(() => _ = links.get_Alias(999));
        AssertBadIndex(() => _ = links.get_DistributionList(999));
    }

    [TestMethod]
    public void ApplicationLinks_PreservesAdministratorBoundaryAndUsesConfiguredRuntime()
    {
        LinksAdministrationRuntimeHost.Configure(
            new RecordingDomainStore(
                new[] { new DomainAdministrationSnapshot(10, "alpha.example", true) }),
            new RecordingAccountStore(
                new AccountAdministrationSnapshot(20, 10, "user@alpha.example", true, AdminLevel: 0)),
            new RecordingAliasStore(
                new AliasAdministrationSnapshot(30, 10, "sales@alpha.example", "user@alpha.example", true)),
            new RecordingDistributionListStore(
                new DistributionListAdministrationSnapshot(
                    40,
                    10,
                    "team@alpha.example",
                    true,
                    RequireSmtpAuth: false,
                    RequireSenderAddress: string.Empty,
                    Mode: 0)));
        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));

        AssertAccessDenied(() => _ = application.Links);

        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));
        var links = application.Links;

        Assert.AreEqual(10, links.get_Domain(10).ID);
        Assert.AreEqual(20, links.get_Account(20).ID);
        Assert.AreEqual(30, links.get_Alias(30).ID);
        Assert.AreEqual(40, links.get_DistributionList(40).ID);
    }

    private static void AssertAccessDenied(Action action)
    {
        var error = Assert.ThrowsExactly<COMException>(action);
        Assert.AreEqual(EAccessDenied, error.ErrorCode);
    }

    private static void AssertBadIndex(Action action)
    {
        var error = Assert.ThrowsExactly<COMException>(action);
        Assert.AreEqual(DispEBadIndex, error.ErrorCode);
    }

    private sealed class RecordingAdministratorAuthenticationProvider(string password)
        : IServerAdministratorAuthenticationProvider
    {
        public bool Authenticate(string username, string attemptedPassword) =>
            username.Equals("Administrator", StringComparison.OrdinalIgnoreCase)
            && attemptedPassword == password;
    }

    private sealed class RecordingDomainStore(IReadOnlyList<DomainAdministrationSnapshot>? domains = null)
        : IDomainAdministrationStore
    {
        public ValueTask<IReadOnlyList<DomainAdministrationSnapshot>> GetDomainsAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(domains ?? Array.Empty<DomainAdministrationSnapshot>());
    }

    private sealed class RecordingAccountStore(params AccountAdministrationSnapshot[] accounts)
        : IAccountAdministrationStore
    {
        public List<int> DomainIds { get; } = new();

        public ValueTask<IReadOnlyList<AccountAdministrationSnapshot>> GetAccountsAsync(
            int domainId,
            CancellationToken cancellationToken)
        {
            DomainIds.Add(domainId);
            return ValueTask.FromResult<IReadOnlyList<AccountAdministrationSnapshot>>(
                accounts.Where(account => account.DomainId == domainId).ToArray());
        }
    }

    private sealed class RecordingAliasStore(params AliasAdministrationSnapshot[] aliases)
        : IAliasAdministrationStore
    {
        public List<int> DomainIds { get; } = new();

        public ValueTask<IReadOnlyList<AliasAdministrationSnapshot>> GetAliasesAsync(
            int domainId,
            CancellationToken cancellationToken)
        {
            DomainIds.Add(domainId);
            return ValueTask.FromResult<IReadOnlyList<AliasAdministrationSnapshot>>(
                aliases.Where(alias => alias.DomainId == domainId).ToArray());
        }
    }

    private sealed class RecordingDistributionListStore(params DistributionListAdministrationSnapshot[] lists)
        : IDistributionListAdministrationStore
    {
        public List<int> DomainIds { get; } = new();

        public ValueTask<IReadOnlyList<DistributionListAdministrationSnapshot>> GetDistributionListsAsync(
            int domainId,
            CancellationToken cancellationToken)
        {
            DomainIds.Add(domainId);
            return ValueTask.FromResult<IReadOnlyList<DistributionListAdministrationSnapshot>>(
                lists.Where(list => list.DomainId == domainId).ToArray());
        }
    }
}
