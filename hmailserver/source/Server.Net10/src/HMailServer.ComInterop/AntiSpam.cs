using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("998A7E66-21FA-47CC-9DB4-81822F2D05C9")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceAntiSpam
{
    [DispId(1)]
    bool GreyListingEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(2)]
    int GreyListingInitialDelay { get; set; }

    [DispId(3)]
    int GreyListingInitialDelete { get; set; }

    [DispId(4)]
    int GreyListingFinalDelete { get; set; }

    [DispId(6)]
    IInterfaceSURBLServers SURBLServers { get; }

    [DispId(7)]
    bool CheckHostInHelo
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(8)]
    bool AddHeaderSpam
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(9)]
    bool AddHeaderReason
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(10)]
    bool PrependSubject
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(11)]
    string PrependSubjectText
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(12)]
    IInterfaceGreyListingWhiteAddresses GreyListingWhiteAddresses { get; }

    [DispId(13)]
    IInterfaceWhiteListAddresses WhiteListAddresses { get; }

    [DispId(14)]
    int CheckHostInHeloScore { get; set; }

    [DispId(15)]
    int SpamMarkThreshold { get; set; }

    [DispId(16)]
    int SpamDeleteThreshold { get; set; }

    [DispId(17)]
    bool UseSPF
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(18)]
    bool UseMXChecks
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(19)]
    int UseSPFScore { get; set; }

    [DispId(20)]
    int UseMXChecksScore { get; set; }

    [DispId(21)]
    IInterfaceDNSBlackLists DNSBlackLists { get; }

    [DispId(22)]
    int TarpitDelay { get; set; }

    [DispId(23)]
    int TarpitCount { get; set; }

    [DispId(24)]
    bool SpamAssassinEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(25)]
    int SpamAssassinScore { get; set; }

    [DispId(26)]
    bool SpamAssassinMergeScore
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(27)]
    string SpamAssassinHost
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(28)]
    int SpamAssassinPort { get; set; }

    [DispId(29)]
    void ClearGreyListingTriplets();

    [DispId(30)]
    int MaximumMessageSize { get; set; }

    [DispId(31)]
    ComDkimResult DKIMVerify([MarshalAs(UnmanagedType.BStr)] string file);

    [DispId(32)]
    bool DKIMVerificationEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(33)]
    int DKIMVerificationFailureScore { get; set; }

    [DispId(34)]
    bool BypassGreylistingOnSPFSuccess
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(35)]
    bool BypassGreylistingOnMailFromMX
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(36)]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool TestSpamAssassinConnection(
        [MarshalAs(UnmanagedType.BStr)] string hostname,
        int port,
        [MarshalAs(UnmanagedType.BStr)] out string resultText);

    [DispId(37)]
    bool CheckPTR
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(38)]
    int CheckPTRScore { get; set; }
}

[ComVisible(true)]
[Guid("A0B91A99-BCE8-4939-94EC-0881E25A1E5B")]
[ProgId("hMailServer.AntiSpam.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceAntiSpam))]
public sealed class AntiSpam : IInterfaceAntiSpam
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private readonly AntiSpamAdministrationSnapshot? _snapshot;

    public AntiSpam()
    {
    }

    private AntiSpam(AntiSpamAdministrationSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    public bool GreyListingEnabled { get => Snapshot.GreyListingEnabled; set => Unavailable(); }

    public int GreyListingInitialDelay { get => Snapshot.GreyListingInitialDelay; set => Unavailable(); }

    public int GreyListingInitialDelete { get => Snapshot.GreyListingInitialDelete; set => Unavailable(); }

    public int GreyListingFinalDelete { get => Snapshot.GreyListingFinalDelete; set => Unavailable(); }

    public IInterfaceSURBLServers SURBLServers
    {
        get
        {
            _ = Snapshot;
            return SurblServerAdministrationRuntimeHost.CreateAuthorizedAdapter();
        }
    }

    public bool CheckHostInHelo { get => Snapshot.CheckHostInHelo; set => Unavailable(); }

    public bool AddHeaderSpam { get => Snapshot.AddHeaderSpam; set => Unavailable(); }

    public bool AddHeaderReason { get => Snapshot.AddHeaderReason; set => Unavailable(); }

    public bool PrependSubject { get => Snapshot.PrependSubject; set => Unavailable(); }

    public string PrependSubjectText { get => Snapshot.PrependSubjectText; set => Unavailable(); }

    public IInterfaceGreyListingWhiteAddresses GreyListingWhiteAddresses => Unavailable<IInterfaceGreyListingWhiteAddresses>();

    public IInterfaceWhiteListAddresses WhiteListAddresses => Unavailable<IInterfaceWhiteListAddresses>();

    public int CheckHostInHeloScore { get => Snapshot.CheckHostInHeloScore; set => Unavailable(); }

    public int SpamMarkThreshold { get => Snapshot.SpamMarkThreshold; set => Unavailable(); }

    public int SpamDeleteThreshold { get => Snapshot.SpamDeleteThreshold; set => Unavailable(); }

    public bool UseSPF { get => Snapshot.UseSpf; set => Unavailable(); }

    public bool UseMXChecks { get => Snapshot.UseMxChecks; set => Unavailable(); }

    public int UseSPFScore { get => Snapshot.UseSpfScore; set => Unavailable(); }

    public int UseMXChecksScore { get => Snapshot.UseMxChecksScore; set => Unavailable(); }

    public IInterfaceDNSBlackLists DNSBlackLists
    {
        get
        {
            _ = Snapshot;
            return DnsBlackListAdministrationRuntimeHost.CreateAuthorizedAdapter();
        }
    }

    public int TarpitDelay { get { _ = Snapshot; return 0; } set => Unavailable(); }

    public int TarpitCount { get { _ = Snapshot; return 0; } set => Unavailable(); }

    public bool SpamAssassinEnabled { get => Snapshot.SpamAssassinEnabled; set => Unavailable(); }

    public int SpamAssassinScore { get => Snapshot.SpamAssassinScore; set => Unavailable(); }

    public bool SpamAssassinMergeScore { get => Snapshot.SpamAssassinMergeScore; set => Unavailable(); }

    public string SpamAssassinHost { get => Snapshot.SpamAssassinHost; set => Unavailable(); }

    public int SpamAssassinPort { get => Snapshot.SpamAssassinPort; set => Unavailable(); }

    public void ClearGreyListingTriplets() => Unavailable();

    public int MaximumMessageSize { get => Snapshot.MaximumMessageSize; set => Unavailable(); }

    public ComDkimResult DKIMVerify(string file) => Unavailable<ComDkimResult>();

    public bool DKIMVerificationEnabled { get => Snapshot.DkimVerificationEnabled; set => Unavailable(); }

    public int DKIMVerificationFailureScore { get => Snapshot.DkimVerificationFailureScore; set => Unavailable(); }

    public bool BypassGreylistingOnSPFSuccess { get => Snapshot.BypassGreylistingOnSpfSuccess; set => Unavailable(); }

    public bool BypassGreylistingOnMailFromMX { get => Snapshot.BypassGreylistingOnMailFromMx; set => Unavailable(); }

    public bool TestSpamAssassinConnection(string hostname, int port, out string resultText)
    {
        resultText = string.Empty;
        return Unavailable<bool>();
    }

    public bool CheckPTR { get => Snapshot.CheckPtr; set => Unavailable(); }

    public int CheckPTRScore { get => Snapshot.CheckPtrScore; set => Unavailable(); }

    internal static AntiSpam CreateAuthorized(AntiSpamAdministrationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new AntiSpam(snapshot);
    }

    private AntiSpamAdministrationSnapshot Snapshot =>
        _snapshot ?? throw new COMException(
            "AntiSpam access requires an authenticated server administrator.",
            EAccessDenied);

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "AntiSpam mutation, collection, DKIM verification, greylisting cleanup, and SpamAssassin test methods are not implemented in the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private T Unavailable<T>()
    {
        Unavailable();
        return default!;
    }
}
