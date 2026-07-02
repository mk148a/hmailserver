using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("952EE84F-C1D4-4869-8B86-76A3BA8F39FA")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceAntiVirus
{
    [DispId(1)]
    bool ClamWinEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(2)]
    string ClamWinExecutable
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(3)]
    string ClamWinDBFolder
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(4)]
    ComAntivirusAction Action { get; set; }

    [DispId(5)]
    bool NotifyReceiver
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(6)]
    bool NotifySender
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(7)]
    bool CustomScannerEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(8)]
    string CustomScannerExecutable
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(9)]
    int CustomScannerReturnValue { get; set; }

    [DispId(10)]
    int MaximumMessageSize { get; set; }

    [DispId(11)]
    IInterfaceBlockedAttachments BlockedAttachments { get; }

    [DispId(12)]
    bool EnableAttachmentBlocking
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(13)]
    bool ClamAVEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(14)]
    string ClamAVHost
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(15)]
    int ClamAVPort { get; set; }

    [DispId(16)]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool TestCustomerScanner(
        [MarshalAs(UnmanagedType.BStr)] string customExecutable,
        int virusReturnCode,
        [MarshalAs(UnmanagedType.BStr)] out string resultText);

    [DispId(17)]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool TestClamWinScanner(
        [MarshalAs(UnmanagedType.BStr)] string clamWinExecutable,
        [MarshalAs(UnmanagedType.BStr)] string clamWinDatabase,
        [MarshalAs(UnmanagedType.BStr)] out string resultText);

    [DispId(18)]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool TestClamAVScanner(
        [MarshalAs(UnmanagedType.BStr)] string clamAVHostName,
        int clamAVPort,
        [MarshalAs(UnmanagedType.BStr)] out string resultText);
}

[ComVisible(true)]
[Guid("82D6DBF9-DDDB-4C4A-A52A-92B6ED16D8EA")]
[ProgId("hMailServer.AntiVirus.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceAntiVirus))]
public sealed class AntiVirus : IInterfaceAntiVirus
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private readonly AntiVirusAdministrationSnapshot? _snapshot;

    public AntiVirus()
    {
    }

    private AntiVirus(AntiVirusAdministrationSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    public bool ClamWinEnabled { get => Snapshot.ClamWinEnabled; set => Unavailable(); }

    public string ClamWinExecutable { get => Snapshot.ClamWinExecutable; set => Unavailable(); }

    public string ClamWinDBFolder { get => Snapshot.ClamWinDatabase; set => Unavailable(); }

    public ComAntivirusAction Action
    {
        get => Snapshot.Action == (int)ComAntivirusAction.DeleteAttachments
            ? ComAntivirusAction.DeleteAttachments
            : ComAntivirusAction.DeleteEmail;
        set => Unavailable();
    }

    public bool NotifyReceiver { get => Snapshot.NotifyReceiver; set => Unavailable(); }

    public bool NotifySender { get => Snapshot.NotifySender; set => Unavailable(); }

    public bool CustomScannerEnabled { get => Snapshot.CustomScannerEnabled; set => Unavailable(); }

    public string CustomScannerExecutable { get => Snapshot.CustomScannerExecutable; set => Unavailable(); }

    public int CustomScannerReturnValue { get => Snapshot.CustomScannerReturnValue; set => Unavailable(); }

    public int MaximumMessageSize { get => Snapshot.MaximumMessageSize; set => Unavailable(); }

    public IInterfaceBlockedAttachments BlockedAttachments => Unavailable<IInterfaceBlockedAttachments>();

    public bool EnableAttachmentBlocking { get => Snapshot.EnableAttachmentBlocking; set => Unavailable(); }

    public bool ClamAVEnabled { get => Snapshot.ClamAvEnabled; set => Unavailable(); }

    public string ClamAVHost { get => Snapshot.ClamAvHost; set => Unavailable(); }

    public int ClamAVPort { get => Snapshot.ClamAvPort; set => Unavailable(); }

    public bool TestCustomerScanner(string customExecutable, int virusReturnCode, out string resultText)
    {
        resultText = string.Empty;
        return Unavailable<bool>();
    }

    public bool TestClamWinScanner(string clamWinExecutable, string clamWinDatabase, out string resultText)
    {
        resultText = string.Empty;
        return Unavailable<bool>();
    }

    public bool TestClamAVScanner(string clamAVHostName, int clamAVPort, out string resultText)
    {
        resultText = string.Empty;
        return Unavailable<bool>();
    }

    internal static AntiVirus CreateAuthorized(AntiVirusAdministrationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new AntiVirus(snapshot);
    }

    private AntiVirusAdministrationSnapshot Snapshot =>
        _snapshot ?? throw new COMException(
            "AntiVirus access requires an authenticated server administrator.",
            EAccessDenied);

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "AntiVirus mutation and scanner test methods are not implemented in the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private T Unavailable<T>()
    {
        Unavailable();
        return default!;
    }
}
