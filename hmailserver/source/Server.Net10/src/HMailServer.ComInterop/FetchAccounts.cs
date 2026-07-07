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

    public FetchAccounts()
    {
    }

    private FetchAccounts(
        IReadOnlyList<FetchAccountAdministrationSnapshot> accounts,
        Func<IReadOnlyList<FetchAccountAdministrationSnapshot>>? reload)
    {
        _accounts = accounts.ToArray();
        _reload = reload;
    }

    public int Count => GetAccounts().Count;

    internal static FetchAccounts CreateAuthorized(
        IReadOnlyList<FetchAccountAdministrationSnapshot> accounts,
        Func<IReadOnlyList<FetchAccountAdministrationSnapshot>>? reload = null)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        return new FetchAccounts(accounts, reload);
    }

    public IInterfaceFetchAccount get_ItemByDBID(int databaseId)
    {
        var match = GetAccounts().FirstOrDefault(account => account.Id == databaseId);

        return match is null
            ? throw new COMException("No fetch account with the specified database identifier exists.", DispEBadIndex)
            : FetchAccount.CreateAuthorized(match);
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

            return FetchAccount.CreateAuthorized(accounts[index]);
        }
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

    public void Delete(int index) => Unavailable();

    public void DeleteByDBID(int databaseId) => Unavailable();

    public IInterfaceFetchAccount Add() => Unavailable<IInterfaceFetchAccount>();

    private IReadOnlyList<FetchAccountAdministrationSnapshot> GetAccounts()
    {
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
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly FetchAccountAdministrationSnapshot? _account;

    public FetchAccount()
    {
    }

    private FetchAccount(FetchAccountAdministrationSnapshot account)
    {
        _account = account;
    }

    public int ID => Snapshot.Id;

    public string Name { get => Snapshot.Name; set => Unavailable(); }

    public string ServerAddress { get => Snapshot.ServerAddress; set => Unavailable(); }

    public int Port { get => Snapshot.Port; set => Unavailable(); }

    public int ServerType { get => Snapshot.ServerType; set => Unavailable(); }

    public string Username { get => Snapshot.Username; set => Unavailable(); }

    public string Password { get => Unavailable<string>(); set => Unavailable(); }

    public int MinutesBetweenFetch { get => Snapshot.MinutesBetweenFetch; set => Unavailable(); }

    public int DaysToKeepMessages { get => Snapshot.DaysToKeepMessages; set => Unavailable(); }

    public int AccountID { get => Snapshot.AccountId; set => Unavailable(); }

    public bool Enabled { get => Snapshot.Enabled; set => Unavailable(); }

    public bool ProcessMIMERecipients { get => Snapshot.ProcessMimeRecipients; set => Unavailable(); }

    public bool ProcessMIMEDate { get => Snapshot.ProcessMimeDate; set => Unavailable(); }

    public bool UseSSL { get => Snapshot.ConnectionSecurity == (int)ComConnectionSecurity.Tls; set => Unavailable(); }

    public string NextDownloadTime => Snapshot.NextDownloadTime;

    public bool UseAntiSpam { get => Snapshot.UseAntiSpam; set => Unavailable(); }

    public bool UseAntiVirus { get => Snapshot.UseAntiVirus; set => Unavailable(); }

    public bool EnableRouteRecipients { get => Snapshot.EnableRouteRecipients; set => Unavailable(); }

    public bool IsLocked => Snapshot.IsLocked;

    public ComConnectionSecurity ConnectionSecurity
    {
        get => (ComConnectionSecurity)Snapshot.ConnectionSecurity;
        set => Unavailable();
    }

    public string MIMERecipientHeaders { get => Snapshot.MimeRecipientHeaders; set => Unavailable(); }

    internal static FetchAccount CreateAuthorized(FetchAccountAdministrationSnapshot account) => new(account);

    public void Save() => Unavailable();

    public void DownloadNow() => Unavailable();

    public void Delete() => Unavailable();

    private FetchAccountAdministrationSnapshot Snapshot =>
        _account ?? throw new COMException(
            "FetchAccount access requires an authenticated server administrator.",
            EAccessDenied);

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

    public static void Configure(IFetchAccountAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static FetchAccounts CreateAuthorizedAdapter(int accountId)
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

        return FetchAccounts.CreateAuthorized(LoadFetchAccounts(), LoadFetchAccounts);
    }
}
