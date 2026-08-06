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
    private readonly int _domainId;
    private readonly Func<AccountAdministrationSnapshot, string, int>? _insert;
    private readonly Func<IReadOnlyList<AccountAdministrationSnapshot>>? _reload;
    private readonly AccountSizeInvalidator? _accountSizeInvalidator;
    private readonly Func<int, AccountAdministrationSnapshot?>? _accountSizeReadback;
    private readonly Func<bool>? _isAuthenticated;
    private readonly object _accountSizeRegistrationOwner = new();

    public Accounts()
    {
    }

    private Accounts(
        IReadOnlyList<AccountAdministrationSnapshot> accounts,
        Func<IReadOnlyList<AccountAdministrationSnapshot>>? reload,
        int domainId,
        Func<AccountAdministrationSnapshot, string, int>? insert,
        Func<bool>? isAuthenticated,
        AccountSizeInvalidator? accountSizeInvalidator,
        Func<int, AccountAdministrationSnapshot?>? accountSizeReadback)
    {
        _accounts = CreateEntries(accounts);
        _domainId = domainId;
        _insert = insert;
        _reload = reload;
        _accountSizeInvalidator = accountSizeInvalidator;
        _accountSizeReadback = accountSizeReadback;
        _isAuthenticated = isAuthenticated;
        foreach (var account in accounts)
        {
            _accountSizeInvalidator?.Register(_accountSizeRegistrationOwner, account.Id);
        }
    }

    public int Count => GetAccounts().Count;

    internal static Accounts CreateAuthorized(
        IReadOnlyList<AccountAdministrationSnapshot> accounts,
        Func<IReadOnlyList<AccountAdministrationSnapshot>>? reload = null,
        int domainId = 0,
        Func<AccountAdministrationSnapshot, string, int>? insert = null,
        Func<bool>? isAuthenticated = null,
        AccountSizeInvalidator? accountSizeInvalidator = null,
        Func<int, AccountAdministrationSnapshot?>? accountSizeReadback = null)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        return new Accounts(
            accounts,
            reload,
            domainId,
            insert,
            isAuthenticated,
            accountSizeInvalidator,
            accountSizeReadback);
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
                accounts[index].ImapFoldersState,
                _isAuthenticated,
                _accountSizeInvalidator,
                _accountSizeReadback);
        }
    }

        public IInterfaceAccount Add()
    {
        _ = GetAccounts();
        if (_insert is null)
        {
            return Unavailable<IInterfaceAccount>();
        }

        return Account.CreateAuthorizedDraft(
            string.Empty,
            ComAdminLevel.Normal,
            _domainId,
            SaveAccount,
            _isAuthenticated);
    }

    private int SaveAccount(AccountAdministrationSnapshot account, string password)
    {
        var accounts = GetAccounts();
        if (_insert is null)
        {
            Unavailable();
            return 0;
        }

        try
        {
            var insertedId = _insert(account, password);
            if (insertedId <= 0)
            {
                throw new InvalidOperationException(
                    "The account insert did not return a valid generated identity.");
            }

            var saved = account with { Id = insertedId };
            var entry = new AccountAdministrationEntry(
                saved,
                RuleAdministrationRuntimeHost.CreateAuthorizedState(insertedId),
                MessageAdministrationRuntimeHost.CreateAuthorizedAccountState(insertedId),
                ImapFolderAdministrationRuntimeHost.CreateAuthorizedState(insertedId));
            Volatile.Write(ref _accounts, accounts.Append(entry).ToArray());
            _accountSizeInvalidator?.Register(_accountSizeRegistrationOwner, insertedId);
            return insertedId;
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the account to the database.",
                EFail);
        }
    }

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
            _accountSizeInvalidator?.Reconcile(
                _accountSizeRegistrationOwner,
                accounts.Select(account => account.Id).ToArray());
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
                match.ImapFoldersState,
                _isAuthenticated,
                _accountSizeInvalidator,
                _accountSizeReadback);
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
                match.ImapFoldersState,
                _isAuthenticated,
                _accountSizeInvalidator,
                _accountSizeReadback);
    }

    public void DeleteByDBID(int databaseId) => Unavailable();

    private IReadOnlyList<AccountAdministrationEntry> GetAccounts()
    {
        if (_isAuthenticated is not null && !_isAuthenticated())
        {
            throw new COMException("Accounts access requires an authenticated server administrator.", EAccessDenied);
        }

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
    private static readonly AccountSizeInvalidator _accountSizeInvalidator = new();

    public static void Configure(IAccountAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
        _accountSizeInvalidator.Reset();
    }

    public static void InvalidateAccountSize(int accountId) =>
        _accountSizeInvalidator.Invalidate(accountId);

    internal static Accounts CreateAuthorizedAdapter(int domainId, Func<bool>? isAuthenticated = null)
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

        AccountAdministrationSnapshot? ReadAccount(int accountId) => store
            .GetAccountByIdAsync(accountId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        int InsertAccount(AccountAdministrationSnapshot account, string password) => store
            .InsertAccountAsync(domainId, account, password, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return Accounts.CreateAuthorized(
            LoadAccounts(),
            LoadAccounts,
            domainId,
            InsertAccount,
            isAuthenticated,
            _accountSizeInvalidator,
            ReadAccount);
    }

    internal static Account CreateAuthorizedAccountByIdAdapter(int accountId)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer account administration runtime has not been initialized.",
                CoENotInitialized);

        return CreateAuthorizedAccountAdapter(store, accountId);
    }

    internal static Account CreateAuthorizedAccountAdapter(
        IAccountAdministrationStore store,
        int accountId)
    {
        ArgumentNullException.ThrowIfNull(store);

        var account = store
            .GetAccountByIdAsync(accountId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        if (account is null)
        {
            throw new COMException("No account with the specified database identifier exists.", DispEBadIndex);
        }

        _accountSizeInvalidator.Register(account.Id);
        return Account.CreateAuthorized(
            account,
            RuleAdministrationRuntimeHost.CreateAuthorizedState(account.Id),
            MessageAdministrationRuntimeHost.CreateAuthorizedAccountState(account.Id),
            ImapFolderAdministrationRuntimeHost.CreateAuthorizedState(account.Id),
            null,
            _accountSizeInvalidator,
            accountId => store
                .GetAccountByIdAsync(accountId, CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult());
    }
}
