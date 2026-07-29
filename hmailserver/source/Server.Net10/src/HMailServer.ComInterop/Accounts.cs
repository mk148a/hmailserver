using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("0AD49AE7-05ED-45F2-8D5A-68FC964EB7EA")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceAccounts
{
    [DispId(0)]
    IInterfaceAccount this[int index] { get; }

    [DispId(1)]
    int Count { get; }

    [DispId(2)]
    IInterfaceAccount Add();

    [DispId(3)]
    void Delete(int index);

    [DispId(4)]
    void Refresh();

    [DispId(5)]
    [SpecialName]
    IInterfaceAccount get_ItemByDBID(int databaseId);

    [DispId(6)]
    [SpecialName]
    IInterfaceAccount get_ItemByAddress([MarshalAs(UnmanagedType.BStr)] string address);

    [DispId(7)]
    void DeleteByDBID(int databaseId);
}

[ComVisible(true)]
[Guid("403A75B8-499A-44C1-93D3-6A8A460AA88D")]
[ProgId("hMailServer.Accounts.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceAccounts))]
public sealed class Accounts : IInterfaceAccounts
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private AccountAdministrationEntry[]? _accounts;
    private readonly Func<IReadOnlyList<AccountAdministrationSnapshot>>? _reload;

    public Accounts()
    {
    }

    private Accounts(
        IReadOnlyList<AccountAdministrationSnapshot> accounts,
        Func<IReadOnlyList<AccountAdministrationSnapshot>>? reload)
    {
        _accounts = CreateEntries(accounts);
        _reload = reload;
    }

    public int Count => GetAccounts().Count;

    internal static Accounts CreateAuthorized(
        IReadOnlyList<AccountAdministrationSnapshot> accounts,
        Func<IReadOnlyList<AccountAdministrationSnapshot>>? reload = null)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        return new Accounts(accounts, reload);
    }

    public IInterfaceAccount this[int index]
    {
        get
        {
            var accounts = GetAccounts();
            if (index < 0 || index >= accounts.Count)
            {
                throw new COMException("Account index was outside the collection.", DispEBadIndex);
            }

            return Account.CreateAuthorized(
                accounts[index].Snapshot,
                accounts[index].RulesState,
                accounts[index].MessagesState,
                accounts[index].ImapFoldersState);
        }
    }

    public IInterfaceAccount Add() => Unavailable<IInterfaceAccount>();

    public void Delete(int index) => Unavailable();

    public void Refresh()
    {
        _ = GetAccounts();
        if (_reload is null)
        {
            Unavailable();
            return;
        }

        try
        {
            var accounts = _reload();
            ArgumentNullException.ThrowIfNull(accounts);
            Volatile.Write(ref _accounts, CreateEntries(accounts));
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to retrieve a list of accounts from the database.",
                EFail);
        }
    }

    public IInterfaceAccount get_ItemByDBID(int databaseId)
    {
        var match = GetAccounts().FirstOrDefault(account => account.Snapshot.Id == databaseId);

        return match is null
            ? throw new COMException("No account with the specified database identifier exists.", DispEBadIndex)
            : Account.CreateAuthorized(
                match.Snapshot,
                match.RulesState,
                match.MessagesState,
                match.ImapFoldersState);
    }

    public IInterfaceAccount get_ItemByAddress(string address)
    {
        var match = GetAccounts()
            .FirstOrDefault(account => account.Snapshot.Address.Equals(address, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? throw new COMException("No account with the specified address exists.", DispEBadIndex)
            : Account.CreateAuthorized(
                match.Snapshot,
                match.RulesState,
                match.MessagesState,
                match.ImapFoldersState);
    }

    public void DeleteByDBID(int databaseId) => Unavailable();

    private IReadOnlyList<AccountAdministrationEntry> GetAccounts()
    {
        return Volatile.Read(ref _accounts)
            ?? throw new COMException("Accounts access requires an authenticated server administrator.", EAccessDenied);
    }

    private static AccountAdministrationEntry[] CreateEntries(
        IReadOnlyList<AccountAdministrationSnapshot> accounts) =>
        accounts
            .Select(account => new AccountAdministrationEntry(
                account,
                RuleAdministrationRuntimeHost.CreateAuthorizedState(account.Id),
                MessageAdministrationRuntimeHost.CreateAuthorizedAccountState(account.Id),
                ImapFolderAdministrationRuntimeHost.CreateAuthorizedState(account.Id)))
            .ToArray();

    private sealed record AccountAdministrationEntry(
        AccountAdministrationSnapshot Snapshot,
        RuleAdministrationState RulesState,
        AccountMessageAdministrationState MessagesState,
        ImapFolderAdministrationState ImapFoldersState);

    private T Unavailable<T>()
    {
        _ = GetAccounts();
        throw new COMException("This Accounts member is not implemented by the .NET 10 rewrite yet.", ENotImplemented);
    }

    private void Unavailable()
    {
        _ = GetAccounts();
        throw new COMException("This Accounts member is not implemented by the .NET 10 rewrite yet.", ENotImplemented);
    }
}

[ComVisible(false)]
public static class AccountAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);
    private const int DispEBadIndex = unchecked((int)0x8002000B);

    private static IAccountAdministrationStore? _store;

    public static void Configure(IAccountAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static Accounts CreateAuthorizedAdapter(int domainId)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer account administration runtime has not been initialized.",
                CoENotInitialized);

        IReadOnlyList<AccountAdministrationSnapshot> LoadAccounts() => store
                .GetAccountsAsync(domainId, CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();

        return Accounts.CreateAuthorized(LoadAccounts(), LoadAccounts);
    }

    internal static Account CreateAuthorizedAccountByIdAdapter(int accountId)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer account administration runtime has not been initialized.",
                CoENotInitialized);

        var account = store
            .GetAccountByIdAsync(accountId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return account is null
            ? throw new COMException("No account with the specified database identifier exists.", DispEBadIndex)
            : Account.CreateAuthorized(account);
    }
}
