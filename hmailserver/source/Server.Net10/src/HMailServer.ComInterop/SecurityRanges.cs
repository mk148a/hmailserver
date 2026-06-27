using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly IReadOnlyList<SecurityRangeAdministrationSnapshot>? _ranges;

    public SecurityRanges()
    {
    }

    private SecurityRanges(IReadOnlyList<SecurityRangeAdministrationSnapshot> ranges)
    {
        _ranges = ranges.ToArray();
    }

    public int Count => GetRanges().Count;

    internal static SecurityRanges CreateAuthorized(IReadOnlyList<SecurityRangeAdministrationSnapshot> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        return new SecurityRanges(ranges);
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

            return SecurityRange.CreateAuthorized(ranges[index]);
        }
    }

    public IInterfaceSecurityRange get_ItemByDBID(int databaseId)
    {
        var match = GetRanges().FirstOrDefault(range => range.Id == databaseId);

        return match is null
            ? throw new COMException("No security range with the specified database identifier exists.", DispEBadIndex)
            : SecurityRange.CreateAuthorized(match);
    }

    public IInterfaceSecurityRange get_ItemByName(string itemName)
    {
        var match = GetRanges().FirstOrDefault(
            range => string.Equals(range.Name, itemName, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? throw new COMException("No security range with the specified name exists.", DispEBadIndex)
            : SecurityRange.CreateAuthorized(match);
    }

    public void Delete(int index) => Unavailable();

    public void DeleteByDBID(int databaseId) => Unavailable();

    public void Refresh() => Unavailable();

    public IInterfaceSecurityRange Add() => Unavailable<IInterfaceSecurityRange>();

    public void SetDefault() => Unavailable();

    private IReadOnlyList<SecurityRangeAdministrationSnapshot> GetRanges()
    {
        return _ranges
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

    private readonly SecurityRangeAdministrationSnapshot? _range;

    public SecurityRange()
    {
    }

    private SecurityRange(SecurityRangeAdministrationSnapshot range)
    {
        _range = range;
    }

    public int ID => Snapshot.Id;

    public string LowerIP { get => Snapshot.LowerIp; set => Unavailable(); }

    public string UpperIP { get => Snapshot.UpperIp; set => Unavailable(); }

    public bool AllowSMTPConnections { get => HasOption(AllowSmtp); set => Unavailable(); }

    public bool AllowPOP3Connections { get => HasOption(AllowPop3); set => Unavailable(); }

    public int Priority { get => Snapshot.Priority; set => Unavailable(); }

    public bool AllowIMAPConnections { get => HasOption(AllowImap); set => Unavailable(); }

    public string Name { get => Snapshot.Name; set => Unavailable(); }

    public bool RequireAuthForDeliveryToLocal { get => false; set => Unavailable(); }

    public bool RequireAuthForDeliveryToRemote { get => false; set => Unavailable(); }

    public bool AllowDeliveryFromLocalToLocal { get => HasOption(RelayLocalToLocal); set => Unavailable(); }

    public bool AllowDeliveryFromLocalToRemote { get => HasOption(RelayLocalToRemote); set => Unavailable(); }

    public bool AllowDeliveryFromRemoteToLocal { get => HasOption(RelayRemoteToLocal); set => Unavailable(); }

    public bool AllowDeliveryFromRemoteToRemote { get => HasOption(RelayRemoteToRemote); set => Unavailable(); }

    public bool EnableSpamProtection { get => HasOption(SpamProtection); set => Unavailable(); }

    public bool IsForwardingRelay { get => false; set => Unavailable(); }

    public bool EnableAntiVirus { get => HasOption(VirusProtection); set => Unavailable(); }

    public bool Expires { get => Snapshot.Expires; set => Unavailable(); }

    public object ExpiresTime { get => Snapshot.ExpiresTime; set => Unavailable(); }

    public bool RequireSMTPAuthLocalToLocal { get => HasOption(SmtpAuthLocalToLocal); set => Unavailable(); }

    public bool RequireSMTPAuthLocalToExternal { get => HasOption(SmtpAuthLocalToExternal); set => Unavailable(); }

    public bool RequireSMTPAuthExternalToLocal { get => HasOption(SmtpAuthExternalToLocal); set => Unavailable(); }

    public bool RequireSMTPAuthExternalToExternal { get => HasOption(SmtpAuthExternalToExternal); set => Unavailable(); }

    public bool RequireSSLTLSForAuth { get => HasOption(RequireTlsForAuth); set => Unavailable(); }

    internal static SecurityRange CreateAuthorized(SecurityRangeAdministrationSnapshot range) => new(range);

    public void Save() => Unavailable();

    public void Delete() => Unavailable();

    private SecurityRangeAdministrationSnapshot Snapshot =>
        _range ?? throw new COMException(
            "SecurityRange access requires an authenticated server administrator.",
            EAccessDenied);

    private bool HasOption(int option) => (Snapshot.Options & option) == option;

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

    internal static SecurityRanges CreateAuthorizedAdapter()
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer security range administration runtime has not been initialized.",
                CoENotInitialized);

        var ranges = store
            .GetSecurityRangesAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return SecurityRanges.CreateAuthorized(ranges);
    }
}
