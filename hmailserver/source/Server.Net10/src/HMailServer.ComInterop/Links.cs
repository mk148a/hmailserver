using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("E252D063-7E86-4FCE-B702-A5E89E0DFB48")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceLinks
{
    [DispId(1)]
    [SpecialName]
    IInterfaceDomain get_Domain(int databaseId);

    [DispId(2)]
    [SpecialName]
    IInterfaceAccount get_Account(int databaseId);

    [DispId(3)]
    [SpecialName]
    IInterfaceAlias get_Alias(int databaseId);

    [DispId(4)]
    [SpecialName]
    IInterfaceDistributionList get_DistributionList(int databaseId);
}

[ComVisible(true)]
[Guid("88A65C5B-916D-4A79-948A-B0DEE0454804")]
[ProgId("hMailServer.Links.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceLinks))]
public sealed class Links : IInterfaceLinks
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ELegacyComError = unchecked((int)0x800403E9);

    private readonly IDomainAdministrationStore? _domainStore;
    private readonly IAccountAdministrationStore? _accountStore;
    private readonly IAliasAdministrationStore? _aliasStore;
    private readonly IDistributionListAdministrationStore? _distributionListStore;
    private readonly Func<int, IInterfaceAccount>? _accountFactory;
    private readonly Func<bool>? _isServerAdministrator;

    public Links()
    {
    }

    private Links(
        IDomainAdministrationStore domainStore,
        IAccountAdministrationStore accountStore,
        IAliasAdministrationStore aliasStore,
        IDistributionListAdministrationStore distributionListStore,
        Func<int, IInterfaceAccount>? accountFactory,
        Func<bool>? isServerAdministrator)
    {
        _domainStore = domainStore;
        _accountStore = accountStore;
        _aliasStore = aliasStore;
        _distributionListStore = distributionListStore;
        _accountFactory = accountFactory;
        _isServerAdministrator = isServerAdministrator;
    }

    internal static Links CreateAuthorized(
        IDomainAdministrationStore domainStore,
        IAccountAdministrationStore accountStore,
        IAliasAdministrationStore aliasStore,
        IDistributionListAdministrationStore distributionListStore,
        Func<int, IInterfaceAccount>? accountFactory = null,
        Func<bool>? isServerAdministrator = null)
    {
        ArgumentNullException.ThrowIfNull(domainStore);
        ArgumentNullException.ThrowIfNull(accountStore);
        ArgumentNullException.ThrowIfNull(aliasStore);
        ArgumentNullException.ThrowIfNull(distributionListStore);

        return new Links(
            domainStore,
            accountStore,
            aliasStore,
            distributionListStore,
            accountFactory,
            isServerAdministrator);
    }

    public IInterfaceDomain get_Domain(int databaseId)
    {
        EnsureAuthorized();
        var domain = GetDomains().FirstOrDefault(candidate => candidate.Id == databaseId);
        return domain is null
            ? throw BadIndex("domain")
            : Domain.CreateAuthorized(domain);
    }

    public IInterfaceAccount get_Account(int databaseId)
    {
        EnsureAuthorized();
        var stores = GetStores();
        foreach (var domain in GetDomains(stores.DomainStore))
        {
            var account = GetResult(stores.AccountStore.GetAccountsAsync(domain.Id, CancellationToken.None))
                .FirstOrDefault(candidate => candidate.Id == databaseId);
            if (account is not null)
            {
                return _accountFactory?.Invoke(databaseId) ?? Account.CreateAuthorized(account);
            }
        }

        throw BadIndex("account");
    }

    public IInterfaceAlias get_Alias(int databaseId)
    {
        EnsureAuthorized();
        var stores = GetStores();
        foreach (var domain in GetDomains(stores.DomainStore))
        {
            var alias = GetResult(stores.AliasStore.GetAliasesAsync(domain.Id, CancellationToken.None))
                .FirstOrDefault(candidate => candidate.Id == databaseId);
            if (alias is not null)
            {
                return Alias.CreateAuthorized(alias);
            }
        }

        throw BadIndex("alias");
    }

    public IInterfaceDistributionList get_DistributionList(int databaseId)
    {
        EnsureAuthorized();
        var stores = GetStores();
        foreach (var domain in GetDomains(stores.DomainStore))
        {
            var list = GetResult(
                    stores.DistributionListStore.GetDistributionListsAsync(domain.Id, CancellationToken.None))
                .FirstOrDefault(candidate => candidate.Id == databaseId);
            if (list is not null)
            {
                return DistributionList.CreateAuthorized(list);
            }
        }

        throw BadIndex("distribution list");
    }

    private IReadOnlyList<DomainAdministrationSnapshot> GetDomains()
    {
        var stores = GetStores();
        return GetDomains(stores.DomainStore);
    }

    private static IReadOnlyList<DomainAdministrationSnapshot> GetDomains(
        IDomainAdministrationStore domainStore) =>
        GetResult(domainStore.GetDomainsAsync(CancellationToken.None));

    private void EnsureAuthorized()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "You do not have access to this property / method. Ensure that hMailServer.Application.Authenticate() is called with proper login credentials.",
                ELegacyComError);
        }
    }

    private Stores GetStores()
    {
        if (_domainStore is null
            || _accountStore is null
            || _aliasStore is null
            || _distributionListStore is null)
        {
            throw new COMException(
                "Links access requires an authenticated server administrator.",
                EAccessDenied);
        }

        return new Stores(_domainStore, _accountStore, _aliasStore, _distributionListStore);
    }

    private static IReadOnlyList<T> GetResult<T>(ValueTask<IReadOnlyList<T>> operation) =>
        operation.AsTask().GetAwaiter().GetResult();

    private static COMException BadIndex(string objectName) =>
        new($"No {objectName} with the specified database identifier exists.", DispEBadIndex);

    private sealed record Stores(
        IDomainAdministrationStore DomainStore,
        IAccountAdministrationStore AccountStore,
        IAliasAdministrationStore AliasStore,
        IDistributionListAdministrationStore DistributionListStore);
}

[ComVisible(false)]
public static class LinksAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static Stores? _stores;

    public static void Configure(
        IDomainAdministrationStore domainStore,
        IAccountAdministrationStore accountStore,
        IAliasAdministrationStore aliasStore,
        IDistributionListAdministrationStore distributionListStore)
    {
        ArgumentNullException.ThrowIfNull(domainStore);
        ArgumentNullException.ThrowIfNull(accountStore);
        ArgumentNullException.ThrowIfNull(aliasStore);
        ArgumentNullException.ThrowIfNull(distributionListStore);

        Volatile.Write(
            ref _stores,
            new Stores(domainStore, accountStore, aliasStore, distributionListStore));
    }

    internal static Links CreateAuthorizedAdapter(Func<bool> isServerAdministrator)
    {
        ArgumentNullException.ThrowIfNull(isServerAdministrator);

        var stores = Volatile.Read(ref _stores)
            ?? throw new COMException(
                "The hMailServer links administration runtime has not been initialized.",
                CoENotInitialized);

        return Links.CreateAuthorized(
            stores.DomainStore,
            stores.AccountStore,
            stores.AliasStore,
            stores.DistributionListStore,
            accountFactory: accountId => AccountAdministrationRuntimeHost.CreateAuthorizedAccountAdapter(
                stores.AccountStore,
                accountId),
            isServerAdministrator: isServerAdministrator);
    }

    private sealed record Stores(
        IDomainAdministrationStore DomainStore,
        IAccountAdministrationStore AccountStore,
        IAliasAdministrationStore AliasStore,
        IDistributionListAdministrationStore DistributionListStore);
}
