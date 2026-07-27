using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Net;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("3F0053E1-2328-452F-855D-87FF63E06BE0")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceSecurityRanges
{
    [DispId(0)]
    IInterfaceSecurityRange this[int index] { get; }

    [DispId(1)]
    [SpecialName]
    IInterfaceSecurityRange get_ItemByDBID(int databaseId);

    [DispId(2)]
    void Delete(int index);

    [DispId(3)]
    void DeleteByDBID(int databaseId);

    [DispId(4)]
    void Refresh();

    [DispId(5)]
    IInterfaceSecurityRange Add();

    [DispId(6)]
    int Count { get; }

    [DispId(7)]
    [SpecialName]
    IInterfaceSecurityRange get_ItemByName([MarshalAs(UnmanagedType.BStr)] string itemName);

    [DispId(8)]
    void SetDefault();
}

[ComVisible(true)]
[Guid("3B1CB89D-9248-413D-BF2A-F000E6DB5F54")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceSecurityRange
{
    [DispId(0)]
    int ID { get; }

    [DispId(1)]
    string LowerIP { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(2)]
    string UpperIP { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(3)]
    bool AllowSMTPConnections
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(5)]
    bool AllowPOP3Connections
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(6)]
    int Priority { get; set; }

    [DispId(10)]
    void Save();

    [DispId(11)]
    bool AllowIMAPConnections
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(12)]
    string Name { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(13)]
    bool RequireAuthForDeliveryToLocal
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(14)]
    bool RequireAuthForDeliveryToRemote
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(15)]
    bool AllowDeliveryFromLocalToLocal
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(16)]
    bool AllowDeliveryFromLocalToRemote
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(17)]
    bool AllowDeliveryFromRemoteToLocal
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(18)]
    bool AllowDeliveryFromRemoteToRemote
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(19)]
    bool EnableSpamProtection
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(20)]
    bool IsForwardingRelay
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(21)]
    bool EnableAntiVirus
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(22)]
    void Delete();

    [DispId(23)]
    bool Expires
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(24)]
    object ExpiresTime { [return: MarshalAs(UnmanagedType.Struct)] get; [param: MarshalAs(UnmanagedType.Struct)] set; }

    [DispId(25)]
    bool RequireSMTPAuthLocalToLocal
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(26)]
    bool RequireSMTPAuthLocalToExternal
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(27)]
    bool RequireSMTPAuthExternalToLocal
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(28)]
    bool RequireSMTPAuthExternalToExternal
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(29)]
    bool RequireSSLTLSForAuth
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }
}

[ComVisible(true)]
[Guid("60A752A2-1197-4841-ADD4-CE922873E794")]
[ProgId("hMailServer.SecurityRanges.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceSecurityRanges))]
public sealed class SecurityRanges : IInterfaceSecurityRanges
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private SecurityRangeAdministrationSnapshot[]? _ranges;
    private readonly Func<IReadOnlyList<SecurityRangeAdministrationSnapshot>>? _reload;
    private readonly Action<int>? _deleteById;
    private readonly Func<SecurityRangeAdministrationSnapshot, int>? _insert;
    private readonly Func<bool>? _isServerAdministrator;

    public SecurityRanges()
    {
    }

    private SecurityRanges(
        IReadOnlyList<SecurityRangeAdministrationSnapshot> ranges,
        Func<IReadOnlyList<SecurityRangeAdministrationSnapshot>>? reload,
        Action<int>? deleteById,
        Func<SecurityRangeAdministrationSnapshot, int>? insert,
        Func<bool>? isServerAdministrator)
    {
        _ranges = ranges.ToArray();
        _reload = reload;
        _deleteById = deleteById;
        _insert = insert;
        _isServerAdministrator = isServerAdministrator;
    }

    public int Count => GetRanges().Count;

    internal static SecurityRanges CreateAuthorized(
        IReadOnlyList<SecurityRangeAdministrationSnapshot> ranges,
        Func<IReadOnlyList<SecurityRangeAdministrationSnapshot>>? reload = null,
        Action<int>? deleteById = null,
        Func<SecurityRangeAdministrationSnapshot, int>? insert = null,
        Func<bool>? isServerAdministrator = null)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        return new SecurityRanges(ranges, reload, deleteById, insert, isServerAdministrator);
    }

    public IInterfaceSecurityRange this[int index]
    {
        get
        {
            var ranges = GetRanges();
            if (index < 0 || index >= ranges.Count)
            {
                throw new COMException("Security range index was outside the collection.", DispEBadIndex);
            }

            return SecurityRange.CreateAuthorized(
                ranges[index],
                delete: DeleteByDBID,
                isServerAdministrator: _isServerAdministrator);
        }
    }

    public IInterfaceSecurityRange get_ItemByDBID(int databaseId)
    {
        var match = GetRanges().FirstOrDefault(range => range.Id == databaseId);

        return match is null
            ? throw new COMException("No security range with the specified database identifier exists.", DispEBadIndex)
            : SecurityRange.CreateAuthorized(
                match,
                delete: DeleteByDBID,
                isServerAdministrator: _isServerAdministrator);
    }

    public IInterfaceSecurityRange get_ItemByName(string itemName)
    {
        var match = GetRanges().FirstOrDefault(
            range => string.Equals(range.Name, itemName, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? throw new COMException("No security range with the specified name exists.", DispEBadIndex)
            : SecurityRange.CreateAuthorized(
                match,
                delete: DeleteByDBID,
                isServerAdministrator: _isServerAdministrator);
    }

    public void Delete(int index) => Unavailable();

    public void DeleteByDBID(int databaseId)
    {
        var ranges = GetRanges();
        if (_deleteById is null)
        {
            Unavailable();
            return;
        }

        if (!ranges.Any(range => range.Id == databaseId))
        {
            return;
        }

        try
        {
            _deleteById(databaseId);
            Volatile.Write(
                ref _ranges,
                ranges.Where(range => range.Id != databaseId).ToArray());
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to delete the security range from the database.",
                EFail);
        }
    }

    public void Refresh()
    {
        _ = GetRanges();
        if (_reload is null)
        {
            Unavailable();
            return;
        }

        try
        {
            var ranges = _reload();
            ArgumentNullException.ThrowIfNull(ranges);
            Volatile.Write(ref _ranges, ranges.ToArray());
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to retrieve a list of security ranges from the database.",
                EFail);
        }
    }

    public IInterfaceSecurityRange Add()
    {
        _ = GetRanges();
        if (_insert is null)
        {
            return Unavailable<IInterfaceSecurityRange>();
        }

        return SecurityRange.CreateAuthorized(
            new SecurityRangeAdministrationSnapshot(
                Id: 0,
                Name: string.Empty,
                LowerIp: "0.0.0.0",
                UpperIp: "0.0.0.0",
                Priority: 0,
                Options: 0,
                Expires: false,
                ExpiresTime: new DateTime(2001, 1, 1)),
            save: SaveRange,
            delete: DeleteByDBID,
            isServerAdministrator: _isServerAdministrator);
    }

    public void SetDefault() => Unavailable();

    private SecurityRangeAdministrationSnapshot SaveRange(SecurityRangeAdministrationSnapshot range)
    {
        var ranges = GetRanges();
        if (range.Id != 0 || _insert is null)
        {
            Unavailable();
            return range;
        }

        try
        {
            var insertedRange = range with { Id = _insert(range) };
            Volatile.Write(ref _ranges, ranges.Concat([insertedRange]).ToArray());
            return insertedRange;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the security range to the database.",
                EFail);
        }
    }

    private IReadOnlyList<SecurityRangeAdministrationSnapshot> GetRanges()
    {
        return Volatile.Read(ref _ranges)
            ?? throw new COMException(
                "SecurityRanges access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private T Unavailable<T>()
    {
        _ = GetRanges();
        throw new COMException(
            "This SecurityRanges member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Unavailable()
    {
        _ = GetRanges();
        throw new COMException(
            "This SecurityRanges member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(true)]
[Guid("B149383D-151C-4585-99F8-71876D0F14C4")]
[ProgId("hMailServer.SecurityRange.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceSecurityRange))]
public sealed class SecurityRange : IInterfaceSecurityRange
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private const int AllowSmtp = 1;
    private const int AllowPop3 = 2;
    private const int AllowImap = 8;
    private const int RelayLocalToLocal = 64;
    private const int RelayLocalToRemote = 128;
    private const int RelayRemoteToLocal = 256;
    private const int RelayRemoteToRemote = 512;
    private const int SpamProtection = 1024;
    private const int VirusProtection = 4096;
    private const int SmtpAuthLocalToLocal = 8192;
    private const int SmtpAuthLocalToExternal = 16384;
    private const int SmtpAuthExternalToLocal = 32768;
    private const int SmtpAuthExternalToExternal = 65536;
    private const int RequireTlsForAuth = 131072;

    private SecurityRangeAdministrationSnapshot? _range;
    private readonly Func<SecurityRangeAdministrationSnapshot, SecurityRangeAdministrationSnapshot>? _save;
    private readonly Action<int>? _delete;
    private readonly Func<bool>? _isServerAdministrator;

    public SecurityRange()
    {
    }

    private SecurityRange(
        SecurityRangeAdministrationSnapshot range,
        Func<SecurityRangeAdministrationSnapshot, SecurityRangeAdministrationSnapshot>? save,
        Action<int>? delete,
        Func<bool>? isServerAdministrator)
    {
        _range = range;
        _save = save;
        _delete = delete;
        _isServerAdministrator = isServerAdministrator;
    }

    public int ID => Snapshot.Id;

    public string LowerIP
    {
        get => Snapshot.LowerIp;
        set => Mutate(snapshot => snapshot with { LowerIp = KeepLegacyAddress(snapshot.LowerIp, value) });
    }

    public string UpperIP
    {
        get => Snapshot.UpperIp;
        set => Mutate(snapshot => snapshot with { UpperIp = KeepLegacyAddress(snapshot.UpperIp, value) });
    }

    public bool AllowSMTPConnections { get => HasOption(AllowSmtp); set => SetOption(AllowSmtp, value); }

    public bool AllowPOP3Connections { get => HasOption(AllowPop3); set => SetOption(AllowPop3, value); }

    public int Priority { get => Snapshot.Priority; set => Mutate(snapshot => snapshot with { Priority = value }); }

    public bool AllowIMAPConnections { get => HasOption(AllowImap); set => SetOption(AllowImap, value); }

    public string Name { get => Snapshot.Name; set => Mutate(snapshot => snapshot with { Name = value ?? string.Empty }); }

    public bool RequireAuthForDeliveryToLocal { get => false; set => Mutate(static snapshot => snapshot); }

    public bool RequireAuthForDeliveryToRemote { get => false; set => Mutate(static snapshot => snapshot); }

    public bool AllowDeliveryFromLocalToLocal { get => HasOption(RelayLocalToLocal); set => SetOption(RelayLocalToLocal, value); }

    public bool AllowDeliveryFromLocalToRemote { get => HasOption(RelayLocalToRemote); set => SetOption(RelayLocalToRemote, value); }

    public bool AllowDeliveryFromRemoteToLocal { get => HasOption(RelayRemoteToLocal); set => SetOption(RelayRemoteToLocal, value); }

    public bool AllowDeliveryFromRemoteToRemote { get => HasOption(RelayRemoteToRemote); set => SetOption(RelayRemoteToRemote, value); }

    public bool EnableSpamProtection { get => HasOption(SpamProtection); set => SetOption(SpamProtection, value); }

    public bool IsForwardingRelay { get => false; set => Mutate(static snapshot => snapshot); }

    public bool EnableAntiVirus { get => HasOption(VirusProtection); set => SetOption(VirusProtection, value); }

    public bool Expires { get => Snapshot.Expires; set => Mutate(snapshot => snapshot with { Expires = value }); }

    public object ExpiresTime
    {
        get => Snapshot.ExpiresTime;
        set => Mutate(snapshot => snapshot with { ExpiresTime = NormalizeExpiresTime(value) });
    }

    public bool RequireSMTPAuthLocalToLocal { get => HasOption(SmtpAuthLocalToLocal); set => SetOption(SmtpAuthLocalToLocal, value); }

    public bool RequireSMTPAuthLocalToExternal { get => HasOption(SmtpAuthLocalToExternal); set => SetOption(SmtpAuthLocalToExternal, value); }

    public bool RequireSMTPAuthExternalToLocal { get => HasOption(SmtpAuthExternalToLocal); set => SetOption(SmtpAuthExternalToLocal, value); }

    public bool RequireSMTPAuthExternalToExternal { get => HasOption(SmtpAuthExternalToExternal); set => SetOption(SmtpAuthExternalToExternal, value); }

    public bool RequireSSLTLSForAuth { get => HasOption(RequireTlsForAuth); set => SetOption(RequireTlsForAuth, value); }

    internal static SecurityRange CreateAuthorized(
        SecurityRangeAdministrationSnapshot range,
        Func<SecurityRangeAdministrationSnapshot, SecurityRangeAdministrationSnapshot>? save = null,
        Action<int>? delete = null,
        Func<bool>? isServerAdministrator = null) =>
        new(range, save, delete, isServerAdministrator);

    public void Save()
    {
        EnsureServerAdministrator();
        if (_save is null)
        {
            Unavailable();
            return;
        }

        _range = _save(Snapshot);
    }

    public void Delete()
    {
        EnsureServerAdministrator();
        if (_delete is null)
        {
            Unavailable();
            return;
        }

        _delete(Snapshot.Id);
    }

    private SecurityRangeAdministrationSnapshot Snapshot =>
        _range ?? throw new COMException(
            "SecurityRange access requires an authenticated server administrator.",
            EAccessDenied);

    private bool HasOption(int option) => (Snapshot.Options & option) == option;

    private void SetOption(int option, bool enabled)
    {
        Mutate(snapshot => snapshot with
        {
            Options = enabled
                ? snapshot.Options | option
                : snapshot.Options & ~option
        });
    }

    private void Mutate(Func<SecurityRangeAdministrationSnapshot, SecurityRangeAdministrationSnapshot> mutation)
    {
        if (_save is null)
        {
            Unavailable();
            return;
        }

        _range = mutation(Snapshot);
    }

    private static string KeepLegacyAddress(string current, string? value)
    {
        return IPAddress.TryParse(value ?? string.Empty, out var parsed)
            ? parsed.ToString()
            : current;
    }

    private static DateTime NormalizeExpiresTime(object? value)
    {
        if (value is DateTime dateTime)
        {
            return dateTime;
        }

        return DateTime.TryParse(
                Convert.ToString(value, CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var parsed)
            ? parsed
            : new DateTime(2001, 1, 1);
    }

    private void EnsureServerAdministrator()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "SecurityRange access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "This SecurityRange member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(false)]
public static class SecurityRangeAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static ISecurityRangeAdministrationStore? _store;

    public static void Configure(ISecurityRangeAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static SecurityRanges CreateAuthorizedAdapter(Func<bool>? isServerAdministrator = null)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer security range administration runtime has not been initialized.",
                CoENotInitialized);

        IReadOnlyList<SecurityRangeAdministrationSnapshot> LoadRanges() => store
            .GetSecurityRangesAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        int InsertRange(SecurityRangeAdministrationSnapshot range) => store
            .InsertSecurityRangeAsync(range, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return SecurityRanges.CreateAuthorized(
            LoadRanges(),
            LoadRanges,
            DeleteRangeById,
            InsertRange,
            isServerAdministrator);

        void DeleteRangeById(int databaseId) => store
            .DeleteSecurityRangeByIdAsync(databaseId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }
}
