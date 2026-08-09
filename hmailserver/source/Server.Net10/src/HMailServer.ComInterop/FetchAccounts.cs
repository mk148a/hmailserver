using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("752C1F5E-74DD-424F-AB60-07D9ABB5B7A4")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceFetchAccount
{
    [DispId(1)]
    int ID { get; }

    [DispId(2)]
    string Name { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(3)]
    string ServerAddress { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(4)]
    int Port { get; set; }

    [DispId(5)]
    int ServerType { get; set; }

    [DispId(6)]
    string Username { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(7)]
    string Password { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(8)]
    int MinutesBetweenFetch { get; set; }

    [DispId(9)]
    int DaysToKeepMessages { get; set; }

    [DispId(10)]
    void Save();

    [DispId(11)]
    int AccountID { get; set; }

    [DispId(12)]
    bool Enabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(13)]
    bool ProcessMIMERecipients
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(14)]
    void DownloadNow();

    [DispId(15)]
    bool ProcessMIMEDate
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(16)]
    bool UseSSL
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(17)]
    void Delete();

    [DispId(18)]
    string NextDownloadTime { [return: MarshalAs(UnmanagedType.BStr)] get; }

    [DispId(19)]
    bool UseAntiSpam
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(20)]
    bool UseAntiVirus
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(21)]
    bool EnableRouteRecipients
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(22)]
    bool IsLocked
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;
    }

    [DispId(23)]
    ComConnectionSecurity ConnectionSecurity { get; set; }

    [DispId(24)]
    string MIMERecipientHeaders { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }
}

[ComVisible(true)]
[Guid("1517E0BE-5226-46CC-8C2A-BB16B680FF48")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceFetchAccounts
{
    [DispId(1)]
    int Count { get; }

    [DispId(2)]
    [SpecialName]
    IInterfaceFetchAccount get_ItemByDBID(int databaseId);

    [DispId(3)]
    IInterfaceFetchAccount this[int index] { get; }

    [DispId(5)]
    void Refresh();

    [DispId(6)]
    void Delete(int index);

    [DispId(7)]
    void DeleteByDBID(int databaseId);

    [DispId(8)]
    IInterfaceFetchAccount Add();
}

[ComVisible(true)]
[Guid("F17C3A00-A7A0-4519-AEDD-DCC3B8DE6A3D")]
[ProgId("hMailServer.FetchAccounts.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceFetchAccounts))]
public sealed class FetchAccounts : IInterfaceFetchAccounts
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private FetchAccountAdministrationSnapshot[]? _accounts;
    private readonly Func<IReadOnlyList<FetchAccountAdministrationSnapshot>>? _reload;
    private readonly Func<int, int, ValueTask>? _retryNow;
    private readonly Func<int, int, ValueTask>? _delete;
    private readonly Func<FetchAccountAdministrationDraft, ValueTask<int>>? _insert;
    private readonly int _accountId;
    private readonly Func<bool>? _isAuthenticated;

    public FetchAccounts()
    {
    }

    private FetchAccounts(
        IReadOnlyList<FetchAccountAdministrationSnapshot> accounts,
        Func<IReadOnlyList<FetchAccountAdministrationSnapshot>>? reload,
        Func<int, int, ValueTask>? retryNow,
        Func<int, int, ValueTask>? delete,
        Func<FetchAccountAdministrationDraft, ValueTask<int>>? insert,
        int accountId,
        Func<bool>? isAuthenticated)
    {
        _accounts = accounts.ToArray();
        _reload = reload;
        _retryNow = retryNow;
        _delete = delete;
        _insert = insert;
        _accountId = accountId;
        _isAuthenticated = isAuthenticated;
    }

    public int Count => GetAccounts().Count;

    internal static FetchAccounts CreateAuthorized(
        IReadOnlyList<FetchAccountAdministrationSnapshot> accounts,
        Func<IReadOnlyList<FetchAccountAdministrationSnapshot>>? reload = null,
        Func<int, int, ValueTask>? retryNow = null,
        Func<int, int, ValueTask>? delete = null,
        Func<FetchAccountAdministrationDraft, ValueTask<int>>? insert = null,
        int accountId = 0,
        Func<bool>? isAuthenticated = null)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        return new FetchAccounts(accounts, reload, retryNow, delete, insert, accountId, isAuthenticated);
    }

    public IInterfaceFetchAccount get_ItemByDBID(int databaseId)
    {
        var match = GetAccounts().FirstOrDefault(account => account.Id == databaseId);

        return match is null
            ? throw new COMException("No fetch account with the specified database identifier exists.", DispEBadIndex)
            : FetchAccount.CreateAuthorized(
                match,
                _retryNow,
                _delete is null ? null : DeleteSelectedAsync,
                _isAuthenticated);
    }

    public IInterfaceFetchAccount this[int index]
    {
        get
        {
            var accounts = GetAccounts();
            if (index < 0 || index >= accounts.Count)
            {
                throw new COMException("Fetch account index was outside the collection.", DispEBadIndex);
            }

            return FetchAccount.CreateAuthorized(
                accounts[index],
                _retryNow,
                _delete is null ? null : DeleteSelectedAsync,
                _isAuthenticated);
        }
    }

    private async ValueTask DeleteSelectedAsync(int accountId, int fetchAccountId)
    {
        if (_delete is null)
        {
            Unavailable();
            return;
        }

        var accounts = GetAccounts();
        if (!accounts.Any(account => account.AccountId == accountId && account.Id == fetchAccountId))
        {
            return;
        }

        await _delete(accountId, fetchAccountId).ConfigureAwait(false);

        Volatile.Write(
            ref _accounts,
            accounts
                .Where(account => account.AccountId != accountId || account.Id != fetchAccountId)
                .ToArray());
    }

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
            Volatile.Write(ref _accounts, accounts.ToArray());
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to retrieve a list of fetch accounts from the database.",
                EFail);
        }
    }

    public void Delete(int index)
    {
        var accounts = GetAccounts();
        if (_delete is null)
        {
            Unavailable();
            return;
        }

        if (index < 0 || index >= accounts.Count)
        {
            return;
        }

        var selected = accounts[index];
        try
        {
            DeleteSelectedAsync(selected.AccountId, selected.Id).GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to delete the fetch account from the database.",
                EFail);
        }
    }

    public void DeleteByDBID(int databaseId)
    {
        var accounts = GetAccounts();
        if (_delete is null)
        {
            Unavailable();
            return;
        }

        var selected = accounts.FirstOrDefault(account => account.Id == databaseId);
        if (selected is null)
        {
            return;
        }

        try
        {
            DeleteSelectedAsync(selected.AccountId, selected.Id).GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to delete the fetch account from the database.",
                EFail);
        }
    }

    public IInterfaceFetchAccount Add()
    {
        _ = GetAccounts();
        if (_insert is null)
        {
            Unavailable();
        }

        return FetchAccount.CreateAuthorized(
            new FetchAccountAdministrationDraft(_accountId),
            InsertSelectedAsync,
            _isAuthenticated);
    }

    private async ValueTask<FetchAccountAdministrationSnapshot> InsertSelectedAsync(
        FetchAccountAdministrationDraft draft)
    {
        var insert = _insert ?? throw new COMException(
            "This FetchAccounts member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);

        var id = await insert(draft).ConfigureAwait(false);
        if (id <= 0)
        {
            throw new InvalidOperationException("The fetch-account store did not return a generated identifier.");
        }

        var snapshot = new FetchAccountAdministrationSnapshot(
            Id: id,
            AccountId: draft.AccountId,
            Name: draft.Name,
            ServerAddress: draft.ServerAddress,
            Port: draft.Port,
            ServerType: draft.ServerType,
            Username: draft.Username,
            MinutesBetweenFetch: draft.MinutesBetweenFetch,
            DaysToKeepMessages: draft.DaysToKeepMessages,
            Enabled: draft.Enabled,
            ProcessMimeRecipients: draft.ProcessMimeRecipients,
            ProcessMimeDate: draft.ProcessMimeDate,
            ConnectionSecurity: draft.ConnectionSecurity,
            UseAntiSpam: draft.UseAntiSpam,
            UseAntiVirus: draft.UseAntiVirus,
            EnableRouteRecipients: draft.EnableRouteRecipients,
            MimeRecipientHeaders: draft.MimeRecipientHeaders,
            NextDownloadTime: string.Empty,
            IsLocked: false);

        var accounts = GetAccounts();
        Volatile.Write(ref _accounts, accounts.Append(snapshot).ToArray());
        return snapshot;
    }

    private IReadOnlyList<FetchAccountAdministrationSnapshot> GetAccounts()
    {
        if (_isAuthenticated is not null && !_isAuthenticated())
        {
            throw new COMException(
                "FetchAccounts access requires an authenticated server administrator.",
                EAccessDenied);
        }

        return Volatile.Read(ref _accounts)
            ?? throw new COMException(
                "FetchAccounts access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private T Unavailable<T>()
    {
        _ = GetAccounts();
        throw new COMException(
            "This FetchAccounts member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Unavailable()
    {
        _ = GetAccounts();
        throw new COMException(
            "This FetchAccounts member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(true)]
[Guid("6F5E2977-2F51-40B0-847B-DD44C9ACC5A5")]
[ProgId("hMailServer.FetchAccount.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceFetchAccount))]
public sealed class FetchAccount : IInterfaceFetchAccount
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private FetchAccountAdministrationSnapshot? _account;
    private FetchAccountAdministrationDraft? _draft;
    private readonly Func<int, int, ValueTask>? _retryNow;
    private readonly Func<int, int, ValueTask>? _delete;
    private readonly Func<FetchAccountAdministrationDraft, ValueTask<FetchAccountAdministrationSnapshot>>? _insert;
    private readonly Func<bool>? _isAuthenticated;

    public FetchAccount()
    {
    }

    private FetchAccount(
        FetchAccountAdministrationSnapshot account,
        Func<int, int, ValueTask>? retryNow,
        Func<int, int, ValueTask>? delete,
        Func<bool>? isAuthenticated)
    {
        _account = account;
        _retryNow = retryNow;
        _delete = delete;
        _isAuthenticated = isAuthenticated;
    }

    private FetchAccount(
        FetchAccountAdministrationDraft draft,
        Func<FetchAccountAdministrationDraft, ValueTask<FetchAccountAdministrationSnapshot>> insert,
        Func<bool>? isAuthenticated)
    {
        _draft = draft;
        _insert = insert;
        _isAuthenticated = isAuthenticated;
    }

    public int ID => _draft is { } ? 0 : Snapshot.Id;

    public string Name { get => _draft?.Name ?? Snapshot.Name; set => Stage(value, static (draft, value) => draft with { Name = value }); }

    public string ServerAddress { get => _draft?.ServerAddress ?? Snapshot.ServerAddress; set => Stage(value, static (draft, value) => draft with { ServerAddress = value }); }

    public int Port { get => _draft?.Port ?? Snapshot.Port; set => Stage(value, static (draft, value) => draft with { Port = value }); }

    public int ServerType { get => _draft?.ServerType ?? Snapshot.ServerType; set => Stage(value, static (draft, value) => draft with { ServerType = value }); }

    public string Username { get => _draft?.Username ?? Snapshot.Username; set => Stage(value, static (draft, value) => draft with { Username = value }); }

    public string Password => _draft?.Password ?? Unavailable<string>();

    string IInterfaceFetchAccount.Password
    {
        get => _draft?.Password ?? Unavailable<string>();
        set => Stage(value, static (draft, value) => draft with { Password = value });
    }

    public int MinutesBetweenFetch { get => _draft?.MinutesBetweenFetch ?? Snapshot.MinutesBetweenFetch; set => Stage(value, static (draft, value) => draft with { MinutesBetweenFetch = value }); }

    public int DaysToKeepMessages { get => _draft?.DaysToKeepMessages ?? Snapshot.DaysToKeepMessages; set => Stage(value, static (draft, value) => draft with { DaysToKeepMessages = value }); }

    public int AccountID { get => _draft?.AccountId ?? Snapshot.AccountId; set => Stage(value, static (draft, value) => draft with { AccountId = value }); }

    public bool Enabled { get => _draft?.Enabled ?? Snapshot.Enabled; set => Stage(value, static (draft, value) => draft with { Enabled = value }); }

    public bool ProcessMIMERecipients { get => _draft?.ProcessMimeRecipients ?? Snapshot.ProcessMimeRecipients; set => Stage(value, static (draft, value) => draft with { ProcessMimeRecipients = value }); }

    public bool ProcessMIMEDate { get => _draft?.ProcessMimeDate ?? Snapshot.ProcessMimeDate; set => Stage(value, static (draft, value) => draft with { ProcessMimeDate = value }); }

    public bool UseSSL { get => (_draft?.ConnectionSecurity ?? Snapshot.ConnectionSecurity) == (int)ComConnectionSecurity.Tls; set => Stage(value, static (draft, value) => draft with { ConnectionSecurity = value ? (int)ComConnectionSecurity.Tls : (int)ComConnectionSecurity.None }); }

    public string NextDownloadTime => _draft is { } ? string.Empty : Snapshot.NextDownloadTime;

    public bool UseAntiSpam { get => _draft?.UseAntiSpam ?? Snapshot.UseAntiSpam; set => Stage(value, static (draft, value) => draft with { UseAntiSpam = value }); }

    public bool UseAntiVirus { get => _draft?.UseAntiVirus ?? Snapshot.UseAntiVirus; set => Stage(value, static (draft, value) => draft with { UseAntiVirus = value }); }

    public bool EnableRouteRecipients { get => _draft?.EnableRouteRecipients ?? Snapshot.EnableRouteRecipients; set => Stage(value, static (draft, value) => draft with { EnableRouteRecipients = value }); }

    public bool IsLocked => _draft is { } ? false : Snapshot.IsLocked;

    public ComConnectionSecurity ConnectionSecurity
    {
        get => (ComConnectionSecurity)(_draft?.ConnectionSecurity ?? Snapshot.ConnectionSecurity);
        set => Stage(value, static (draft, value) => draft with { ConnectionSecurity = (int)value });
    }

    public string MIMERecipientHeaders { get => _draft?.MimeRecipientHeaders ?? Snapshot.MimeRecipientHeaders; set => Stage(value, static (draft, value) => draft with { MimeRecipientHeaders = value }); }

    internal static FetchAccount CreateAuthorized(
        FetchAccountAdministrationSnapshot account,
        Func<int, int, ValueTask>? retryNow = null,
        Func<int, int, ValueTask>? delete = null,
        Func<bool>? isAuthenticated = null) => new(account, retryNow, delete, isAuthenticated);

    internal static FetchAccount CreateAuthorized(
        FetchAccountAdministrationDraft draft,
        Func<FetchAccountAdministrationDraft, ValueTask<FetchAccountAdministrationSnapshot>> insert,
        Func<bool>? isAuthenticated = null) =>
        new(draft, insert, isAuthenticated);

    public void Save()
    {
        EnsureAuthenticated();
        var draft = _draft;
        if (draft is null || _insert is null)
        {
            Unavailable();
            return;
        }

        try
        {
            _account = _insert(draft).GetAwaiter().GetResult();
            _draft = null;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the fetch account to the database.",
                EFail);
        }
    }

    public void DownloadNow()
    {
        var account = Snapshot;
        if (_retryNow is null)
        {
            Unavailable();
            return;
        }

        try
        {
            _retryNow(account.AccountId, account.Id).GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to schedule the fetch account for immediate download.",
                EFail);
        }
    }

    public void Delete()
    {
        var account = Snapshot;
        if (_delete is null)
        {
            Unavailable();
            return;
        }

        try
        {
            _delete(account.AccountId, account.Id).GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to delete the fetch account from the database.",
                EFail);
        }
    }

    private FetchAccountAdministrationSnapshot Snapshot
    {
        get
        {
            EnsureAuthenticated();
            return _account ?? throw new COMException(
                "FetchAccount access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private void EnsureAuthenticated()
    {
        if (_isAuthenticated is not null && !_isAuthenticated())
        {
            throw new COMException(
                "FetchAccount access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private void Stage<T>(
        T value,
        Func<FetchAccountAdministrationDraft, T, FetchAccountAdministrationDraft> update)
    {
        EnsureAuthenticated();
        var draft = _draft;
        if (draft is null)
        {
            Unavailable();
            return;
        }

        _draft = update(draft, value);
    }

    private T Unavailable<T>()
    {
        _ = Snapshot;
        throw new COMException(
            "This FetchAccount member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "This FetchAccount member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(false)]
public static class FetchAccountAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static IFetchAccountAdministrationStore? _store;
    private static IExternalFetchWakeSignal? _wakeSignal;

    public static void Configure(
        IFetchAccountAdministrationStore store,
        IExternalFetchWakeSignal? wakeSignal = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
        Volatile.Write(ref _wakeSignal, wakeSignal);
    }

    internal static FetchAccounts CreateAuthorizedAdapter(
        int accountId,
        Func<bool>? isAuthenticated = null)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer fetch-account administration runtime has not been initialized.",
                CoENotInitialized);

        IReadOnlyList<FetchAccountAdministrationSnapshot> LoadFetchAccounts() => store
                .GetFetchAccountsAsync(accountId, CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();

        var wakeSignal = Volatile.Read(ref _wakeSignal);

        async ValueTask RetryNow(int owningAccountId, int fetchAccountId)
        {
            await store
                .SetRetryNowAsync(owningAccountId, fetchAccountId, CancellationToken.None)
                .ConfigureAwait(false);
            wakeSignal?.Signal();
        }

        async ValueTask DeleteFetchAccount(int owningAccountId, int fetchAccountId)
        {
            await store
                .DeleteFetchAccountAsync(owningAccountId, fetchAccountId, CancellationToken.None)
                .ConfigureAwait(false);
        }

        async ValueTask<int> InsertFetchAccount(FetchAccountAdministrationDraft draft)
        {
            if (draft.AccountId != accountId)
            {
                throw new InvalidOperationException("The fetch account draft is outside its owning account.");
            }

            return await store
                .InsertFetchAccountAsync(draft, CancellationToken.None)
                .ConfigureAwait(false);
        }

        return FetchAccounts.CreateAuthorized(
            LoadFetchAccounts(),
            LoadFetchAccounts,
            RetryNow,
            DeleteFetchAccount,
            InsertFetchAccount,
            accountId,
            isAuthenticated);
    }
}
