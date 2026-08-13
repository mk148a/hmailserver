using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Pop3;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class LinksComContractTests
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ELegacyComError = unchecked((int)0x800403E9);
    private const string LegacyAccessDeniedMessage =
        "You do not have access to this property / method. Ensure that hMailServer.Application.Authenticate() is called with proper login credentials.";

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
    public void ApplicationLinksAccount_PropagatesAuthorizationLeaseToFetchMutation()
    {
        var fetchStore = new LinkFetchStore(
            new[]
            {
                new FetchAccountAdministrationSnapshot(
                    Id: 10,
                    AccountId: 20,
                    Name: "External POP3",
                    ServerAddress: "pop.example.test",
                    Port: 110,
                    ServerType: 0,
                    Username: "user",
                    MinutesBetweenFetch: 5,
                    DaysToKeepMessages: 0,
                    Enabled: true,
                    ProcessMimeRecipients: false,
                    ProcessMimeDate: false,
                    ConnectionSecurity: 0,
                    UseAntiSpam: false,
                    UseAntiVirus: false,
                    EnableRouteRecipients: false,
                    MimeRecipientHeaders: string.Empty,
                    NextDownloadTime: string.Empty,
                    IsLocked: false)
            });
        var wakeSignal = new LinkWakeSignal();
        FetchAccountAdministrationRuntimeHost.Configure(fetchStore, wakeSignal);
        LinksAdministrationRuntimeHost.Configure(
            new RecordingDomainStore(new[] { new DomainAdministrationSnapshot(10, "alpha.example", true) }),
            new RecordingAccountStore(
                new AccountAdministrationSnapshot(20, 10, "user@alpha.example", true, AdminLevel: 0)),
            new RecordingAliasStore(),
            new RecordingDistributionListStore());

        var disposed = 0;
        var links = LinksAdministrationRuntimeHost.CreateAuthorizedAdapter(
            () => true,
            _ => ValueTask.FromResult<IDisposable?>(new Lease(() => disposed++)));

        links.get_Account(20).FetchAccounts[0].DownloadNow();

        Assert.AreEqual(1, disposed);
        Assert.AreEqual(1, wakeSignal.SignalCount);
        Assert.AreEqual(10, fetchStore.RetryFetchAccountId);
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
    public async Task DirectLinksFallbackAccount_UnlockMailbox_ReleasesMailboxLock()
    {
        var lockManager = new InMemoryPop3MailboxLockManager();
        AccountAdministrationRuntimeHost.Configure(
            new RecordingAccountStore(
                new AccountAdministrationSnapshot(20, 10, "user@alpha.example", true, AdminLevel: 0)),
            lockManager.Unlock);
        IInterfaceLinks links = Links.CreateAuthorized(
            new RecordingDomainStore(
                new[] { new DomainAdministrationSnapshot(10, "alpha.example", true) }),
            new RecordingAccountStore(
                new AccountAdministrationSnapshot(20, 10, "user@alpha.example", true, AdminLevel: 0)),
            new RecordingAliasStore(),
            new RecordingDistributionListStore(),
            isServerAdministrator: () => true);

        var account = links.get_Account(20);
        var lease = await lockManager.TryAcquireAsync(
            new ImapAuthenticatedAccount(20, "user@alpha.example"),
            CancellationToken.None);

        Assert.IsNotNull(lease);
        account.UnlockMailbox();

        var reacquiredLease = await lockManager.TryAcquireAsync(
            new ImapAuthenticatedAccount(20, "user@alpha.example"),
            CancellationToken.None);
        Assert.IsNotNull(reacquiredLease);
        await lease.DisposeAsync();
        await reacquiredLease.DisposeAsync();
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

    [TestMethod]
    public void ApplicationLinks_RetainedObjectRechecksAuthorizationAcrossFailedAndSuccessfulReauthentication()
    {
        var domains = new RecordingDomainStore(
            new[] { new DomainAdministrationSnapshot(10, "alpha.example", true) });
        var accounts = new RecordingAccountStore(
            new AccountAdministrationSnapshot(20, 10, "user@alpha.example", true, AdminLevel: 0));
        var aliases = new RecordingAliasStore(
            new AliasAdministrationSnapshot(30, 10, "sales@alpha.example", "user@alpha.example", true));
        var lists = new RecordingDistributionListStore(
            new DistributionListAdministrationSnapshot(
                40,
                10,
                "team@alpha.example",
                true,
                RequireSmtpAuth: false,
                RequireSenderAddress: string.Empty,
                Mode: 0));
        LinksAdministrationRuntimeHost.Configure(domains, accounts, aliases, lists);
        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));
        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));
        IInterfaceLinks links = application.Links;

        Assert.IsNull(application.Authenticate("Administrator", "wrong"));

        AssertLegacyComError(() => _ = links.get_Domain(10));
        AssertLegacyComError(() => _ = links.get_Account(20));
        AssertLegacyComError(() => _ = links.get_Alias(30));
        AssertLegacyComError(() => _ = links.get_DistributionList(40));
        Assert.AreEqual(0, domains.ReadCount);
        CollectionAssert.AreEqual(Array.Empty<int>(), accounts.DomainIds.ToArray());
        CollectionAssert.AreEqual(Array.Empty<int>(), aliases.DomainIds.ToArray());
        CollectionAssert.AreEqual(Array.Empty<int>(), lists.DomainIds.ToArray());

        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));

        AssertLegacyComError(() => _ = links.get_Domain(10));
        AssertLegacyComError(() => _ = links.get_Account(20));
        AssertLegacyComError(() => _ = links.get_Alias(30));
        AssertLegacyComError(() => _ = links.get_DistributionList(40));

        var newLinks = application.Links;
        Assert.AreEqual(10, newLinks.get_Domain(10).ID);
        Assert.AreEqual(20, newLinks.get_Account(20).ID);
        Assert.AreEqual(30, newLinks.get_Alias(30).ID);
        Assert.AreEqual(40, newLinks.get_DistributionList(40).ID);
    }

    [TestMethod]
    public void ApplicationLinks_RetainedChildrenRecheckAuthorizationWithoutChildStoreAccess()
    {
        var domains = new RecordingDomainStore(
            new[] { new DomainAdministrationSnapshot(10, "alpha.example", true) });
        var accounts = new RecordingAccountStore(
            new AccountAdministrationSnapshot(20, 10, "user@alpha.example", true, AdminLevel: 0));
        var aliases = new RecordingAliasStore(
            new AliasAdministrationSnapshot(30, 10, "sales@alpha.example", "user@alpha.example", true));
        var lists = new RecordingDistributionListStore(
            new DistributionListAdministrationSnapshot(
                40,
                10,
                "team@alpha.example",
                true,
                RequireSmtpAuth: false,
                RequireSenderAddress: string.Empty,
                Mode: 0));
        AccountAdministrationRuntimeHost.Configure(accounts);
        LinksAdministrationRuntimeHost.Configure(domains, accounts, aliases, lists);
        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));
        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));

        IInterfaceDomain domain = application.Links.get_Domain(10);
        IInterfaceAccount account = application.Links.get_Account(20);
        IInterfaceAlias alias = application.Links.get_Alias(30);
        IInterfaceDistributionList list = application.Links.get_DistributionList(40);
        var domainReads = domains.ReadCount;
        var accountDomainReads = accounts.DomainIds.Count;
        var accountByIdReads = accounts.AccountByIdReadCount;
        var aliasDomainReads = aliases.DomainIds.Count;
        var listDomainReads = lists.DomainIds.Count;

        var standaloneAuthenticated = true;
        var standaloneList = DistributionLists.CreateAuthorized(
            new[]
            {
                new DistributionListAdministrationSnapshot(
                    40,
                    10,
                    "team@alpha.example",
                    true,
                    RequireSmtpAuth: false,
                    RequireSenderAddress: string.Empty,
                    Mode: 0)
            },
            isAuthenticated: () => standaloneAuthenticated)[0];
        standaloneAuthenticated = false;
        Assert.AreEqual("team@alpha.example", standaloneList.Address);

        Assert.IsNull(application.Authenticate("Administrator", "wrong"));

        AssertAccessDenied(() => _ = domain.Name);
        AssertAccessDenied(() => domain.Active = false);
        AssertAccessDenied(() => _ = account.Address);
        AssertAccessDenied(() => _ = account.AdminLevel);
        AssertAccessDenied(() => account.Active = false);
        AssertAccessDenied(() => _ = account.Size);
        AssertAccessDenied(() => _ = alias.Name);
        AssertAccessDenied(() => alias.Active = false);
        AssertAccessDenied(() => _ = list.Address);
        AssertAccessDenied(() => list.Active = false);

        Assert.AreEqual(domainReads, domains.ReadCount);
        Assert.AreEqual(accountDomainReads, accounts.DomainIds.Count);
        Assert.AreEqual(accountByIdReads, accounts.AccountByIdReadCount);
        Assert.AreEqual(aliasDomainReads, aliases.DomainIds.Count);
        Assert.AreEqual(listDomainReads, lists.DomainIds.Count);

        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));

        AssertAccessDenied(() => _ = domain.Name);
        AssertAccessDenied(() => _ = account.Address);
        AssertAccessDenied(() => _ = account.AdminLevel);
        AssertAccessDenied(() => _ = alias.Name);
        AssertAccessDenied(() => _ = list.Address);

        var newLinks = application.Links;
        Assert.AreEqual("alpha.example", newLinks.get_Domain(10).Name);
        Assert.AreEqual("user@alpha.example", newLinks.get_Account(20).Address);
        Assert.AreEqual(ComAdminLevel.Normal, newLinks.get_Account(20).AdminLevel);
        Assert.AreEqual("sales@alpha.example", newLinks.get_Alias(30).Name);
        Assert.AreEqual("team@alpha.example", newLinks.get_DistributionList(40).Address);
    }

    [TestMethod]
    public void ApplicationLinks_AccountUsesSharedAccountSizeInvalidation()
    {
        var accountStore = new RecordingAccountStore(
            new AccountAdministrationSnapshot(
                20,
                10,
                "user@alpha.example",
                true,
                AdminLevel: 0,
                Size: 1.25f,
                QuotaUsed: 10));
        AccountAdministrationRuntimeHost.Configure(accountStore);
        LinksAdministrationRuntimeHost.Configure(
            new RecordingDomainStore(new[] { new DomainAdministrationSnapshot(10, "alpha.example", true) }),
            accountStore,
            new RecordingAliasStore(),
            new RecordingDistributionListStore());

        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));
        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));

        var account = application.Links.get_Account(20);
        Assert.AreEqual(1.25f, account.Size, 0.0001f);

        accountStore.Accounts =
        [
            new AccountAdministrationSnapshot(
                20,
                10,
                "user@alpha.example",
                true,
                AdminLevel: 0,
                Size: 3.5f,
                QuotaUsed: 35)
        ];
        AccountAdministrationRuntimeHost.InvalidateAccountSize(20);

        Assert.AreEqual(3.5f, account.Size, 0.0001f);
        Assert.AreEqual(35, account.QuotaUsed);
    }

    [TestMethod]
    public void ApplicationLinks_DistributionList_PropagatesAuthorizationLeaseToRecipientMutation()
    {
        var recipientStore = new LinkDistributionListRecipientStore();
        DistributionListRecipientAdministrationRuntimeHost.Configure(recipientStore);
        LinksAdministrationRuntimeHost.Configure(
            new RecordingDomainStore(new[] { new DomainAdministrationSnapshot(10, "alpha.example", true) }),
            new RecordingAccountStore(new AccountAdministrationSnapshot(20, 10, "user@alpha.example", true, AdminLevel: 0)),
            new RecordingAliasStore(),
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
        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));

        var list = application.Links.get_DistributionList(40);
        var pending = list.Recipients.Add();
        pending.RecipientAddress = "member@alpha.example";

        Assert.IsNull(application.Authenticate("Administrator", "wrong"));
        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));

        var error = Assert.ThrowsExactly<COMException>(pending.Save);

        Assert.AreEqual(EAccessDenied, error.ErrorCode);
        Assert.AreEqual(0, recipientStore.Inserted.Count);
    }

    [TestMethod]
    public void ApplicationLinks_DistributionList_SharesLifetimeWithDomainDeletion()
    {
        var listStore = new SharedDistributionListStore(
            new DistributionListAdministrationSnapshot(
                40,
                10,
                "team@alpha.example",
                true,
                RequireSmtpAuth: false,
                RequireSenderAddress: string.Empty,
                Mode: 0));
        var recipientStore = new LinkDistributionListRecipientStore();
        DistributionListAdministrationRuntimeHost.Configure(listStore);
        DistributionListRecipientAdministrationRuntimeHost.Configure(recipientStore);
        var domainStore = new RecordingDomainStore(new[] { new DomainAdministrationSnapshot(10, "alpha.example", true) });
        DomainAdministrationRuntimeHost.Configure(domainStore);
        LinksAdministrationRuntimeHost.Configure(
            domainStore,
            new RecordingAccountStore(),
            new RecordingAliasStore(),
            listStore);

        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));
        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));

        var linksList = application.Links.get_DistributionList(40);
        _ = linksList.Recipients;
        application.Domains[0].DistributionLists.DeleteByDBID(40);

        var listError = Assert.ThrowsExactly<COMException>(() => _ = linksList.Address);
        var recipientsError = Assert.ThrowsExactly<COMException>(() => _ = linksList.Recipients.Count);

        Assert.AreEqual(EAccessDenied, listError.ErrorCode);
        Assert.AreEqual(EAccessDenied, recipientsError.ErrorCode);
        Assert.AreEqual(0, recipientStore.Inserted.Count);
    }

    private static void AssertAccessDenied(Action action)
    {
        var error = Assert.ThrowsExactly<COMException>(action);
        Assert.AreEqual(EAccessDenied, error.ErrorCode);
    }

    private static void AssertLegacyComError(Action action)
    {
        var error = Assert.ThrowsExactly<COMException>(action);
        Assert.AreEqual(ELegacyComError, error.ErrorCode);
        Assert.AreEqual(LegacyAccessDeniedMessage, error.Message);
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
        public int ReadCount { get; private set; }

        public ValueTask<IReadOnlyList<DomainAdministrationSnapshot>> GetDomainsAsync(
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult(domains ?? Array.Empty<DomainAdministrationSnapshot>());
        }
    }

    private sealed class RecordingAccountStore(params AccountAdministrationSnapshot[] accounts)
        : IAccountAdministrationStore
    {
        public List<int> DomainIds { get; } = new();

        public int AccountByIdReadCount { get; private set; }

        public IReadOnlyList<AccountAdministrationSnapshot> Accounts { get; set; } = accounts;

        public ValueTask<IReadOnlyList<AccountAdministrationSnapshot>> GetAccountsAsync(
            int domainId,
            CancellationToken cancellationToken)
        {
            DomainIds.Add(domainId);
            return ValueTask.FromResult<IReadOnlyList<AccountAdministrationSnapshot>>(
                Accounts.Where(account => account.DomainId == domainId).ToArray());
        }

        public ValueTask<AccountAdministrationSnapshot?> GetAccountByIdAsync(
            int accountId,
            CancellationToken cancellationToken)
        {
            AccountByIdReadCount++;
            return ValueTask.FromResult(Accounts.FirstOrDefault(account => account.Id == accountId));
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

    private sealed class LinkFetchStore(IReadOnlyList<FetchAccountAdministrationSnapshot> accounts)
        : IFetchAccountAdministrationStore
    {
        public int? RetryFetchAccountId { get; private set; }

        public ValueTask<IReadOnlyList<FetchAccountAdministrationSnapshot>> GetFetchAccountsAsync(
            int accountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<FetchAccountAdministrationSnapshot>>(
                accounts.Where(account => account.AccountId == accountId).ToArray());

        public ValueTask SetRetryNowAsync(
            int accountId,
            int fetchAccountId,
            CancellationToken cancellationToken)
        {
            RetryFetchAccountId = fetchAccountId;
            return ValueTask.CompletedTask;
        }

        public ValueTask<int> InsertFetchAccountAsync(
            FetchAccountAdministrationDraft account,
            CancellationToken cancellationToken) =>
            ValueTask.FromException<int>(new NotSupportedException());

        public ValueTask<bool> UpdateFetchAccountAsync(
            int fetchAccountId,
            FetchAccountAdministrationDraft account,
            string? password,
            CancellationToken cancellationToken) =>
            ValueTask.FromException<bool>(new NotSupportedException());

        public ValueTask DeleteFetchAccountAsync(
            int accountId,
            int fetchAccountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromException(new NotSupportedException());
    }

    private sealed class LinkWakeSignal : IExternalFetchWakeSignal
    {
        public int SignalCount { get; private set; }

        public void Signal() => SignalCount++;

        public ValueTask<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
            ValueTask.FromResult(false);
    }

    private sealed class Lease(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }

    private sealed class LinkDistributionListRecipientStore : IDistributionListRecipientAdministrationStore
    {
        public List<DistributionListRecipientAdministrationSnapshot> Inserted { get; } = [];

        public ValueTask<IReadOnlyList<DistributionListRecipientAdministrationSnapshot>> GetRecipientsAsync(
            int distributionListId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<DistributionListRecipientAdministrationSnapshot>>([]);

        public ValueTask<int> InsertDistributionListRecipientAsync(
            DistributionListRecipientAdministrationSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            Inserted.Add(snapshot);
            return ValueTask.FromResult(901);
        }
    }

    private sealed class SharedDistributionListStore(params DistributionListAdministrationSnapshot[] initial)
        : IDistributionListAdministrationStore
    {
        private readonly List<DistributionListAdministrationSnapshot> _lists = initial.ToList();

        public ValueTask<IReadOnlyList<DistributionListAdministrationSnapshot>> GetDistributionListsAsync(
            int domainId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<DistributionListAdministrationSnapshot>>(
                _lists.Where(list => list.DomainId == domainId).ToArray());

        public ValueTask<int> InsertDistributionListAsync(
            DistributionListAdministrationSnapshot distributionList,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(900 + _lists.Count);

        public ValueTask<bool> UpdateDistributionListAsync(
            DistributionListAdministrationSnapshot distributionList,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(true);

        public ValueTask<bool> DeleteDistributionListAsync(
            int owningDomainId,
            int distributionListId,
            CancellationToken cancellationToken)
        {
            var removed = _lists.RemoveAll(list => list.Id == distributionListId && list.DomainId == owningDomainId);
            return ValueTask.FromResult(removed == 1);
        }
    }
}
