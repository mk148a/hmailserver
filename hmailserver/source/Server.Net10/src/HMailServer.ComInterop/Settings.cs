using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("A4C709A3-98B2-410D-84F4-EDA999BF0CB2")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceSettings
{
    [DispId(5)]
    int MaxSMTPConnections { get; set; }

    [DispId(6)]
    int MaxPOP3Connections { get; set; }

    [DispId(7)]
    string MirrorEMailAddress
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(8)]
    bool AllowSMTPAuthPlain
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(11)]
    bool DenyMailFromNull
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(13)]
    IInterfaceLogging Logging { get; }

    [DispId(18)]
    IInterfaceSecurityRanges SecurityRanges { get; }

    [DispId(19)]
    int SMTPNoOfTries { get; set; }

    [DispId(20)]
    int SMTPMinutesBetweenTry { get; set; }

    [DispId(22)]
    string SMTPRelayer
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(23)]
    string WelcomeSMTP
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(24)]
    string WelcomePOP3
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(25)]
    string WelcomeIMAP
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(26)]
    bool ServiceSMTP
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(27)]
    bool ServicePOP3
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(28)]
    bool ServiceIMAP
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(29)]
    int MaxDeliveryThreads { get; set; }

    [DispId(30)]
    IInterfaceAntiVirus AntiVirus { get; }

    [DispId(31)]
    IInterfaceRoutes Routes { get; }

    [DispId(33)]
    string HostName
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(34)]
    bool SMTPRelayerRequiresAuthentication
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(35)]
    string SMTPRelayerUsername
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(36)]
    void SetSMTPRelayerPassword([MarshalAs(UnmanagedType.BStr)] string newVal);

    [DispId(37)]
    int SMTPRelayerPort { get; set; }

    [DispId(42)]
    string UserInterfaceLanguage
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(43)]
    IInterfaceScripting Scripting { get; }

    [DispId(44)]
    int MaxMessageSize { get; set; }

    [DispId(47)]
    IInterfaceCache Cache { get; }

    [DispId(48)]
    int RuleLoopLimit { get; set; }

    [DispId(49)]
    IInterfaceBackupSettings Backup { get; }

    [DispId(50)]
    string DefaultDomain
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(51)]
    string SMTPDeliveryBindToIP
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(53)]
    int MaxIMAPConnections { get; set; }

    [DispId(54)]
    bool IMAPSortEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(55)]
    bool IMAPQuotaEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(56)]
    bool IMAPIdleEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(57)]
    int WorkerThreadPriority { get; set; }

    [DispId(60)]
    int TCPIPThreads { get; set; }

    [DispId(61)]
    bool AllowIncorrectLineEndings
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(62)]
    int MaxSMTPRecipientsInBatch { get; set; }

    [DispId(63)]
    IInterfaceAntiSpam AntiSpam { get; }

    [DispId(64)]
    bool DisconnectInvalidClients
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(65)]
    int MaxNumberOfInvalidCommands { get; set; }

    [DispId(66)]
    IInterfaceServerMessages ServerMessages { get; }

    [DispId(70)]
    IInterfaceTCPIPPorts TCPIPPorts { get; }

    [DispId(71)]
    bool SMTPRelayerUseSSL
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(72)]
    IInterfaceSSLCertificates SSLCertificates { get; }

    [DispId(73)]
    bool AddDeliveredToHeader
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(74)]
    string IMAPPublicFolderName
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(75)]
    bool IMAPACLEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(76)]
    void SetAdministratorPassword([MarshalAs(UnmanagedType.BStr)] string newVal);

    [DispId(77)]
    IInterfaceDirectories Directories { get; }

    [DispId(78)]
    IInterfaceIMAPFolders PublicFolders { get; }

    [DispId(79)]
    string PublicFolderDiskName
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
    }

    [DispId(80)]
    IInterfaceGroups Groups { get; }

    [DispId(81)]
    IInterfaceIncomingRelays IncomingRelays { get; }

    [DispId(82)]
    bool AutoBanOnLogonFailure
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(83)]
    int MaxInvalidLogonAttempts { get; set; }

    [DispId(84)]
    int MaxInvalidLogonAttemptsWithin { get; set; }

    [DispId(85)]
    int AutoBanMinutes { get; set; }

    [DispId(86)]
    void ClearLogonFailureList();

    [DispId(87)]
    string IMAPHierarchyDelimiter
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(88)]
    int MaxAsynchronousThreads { get; set; }

    [DispId(89)]
    IInterfaceMessageIndexing MessageIndexing { get; }

    [DispId(90)]
    int MaxNumberOfMXHosts { get; set; }

    [DispId(91)]
    ComConnectionSecurity SMTPRelayerConnectionSecurity { get; set; }

    [DispId(92)]
    ComConnectionSecurity SMTPConnectionSecurity { get; set; }

    [DispId(93)]
    bool VerifyRemoteSslCertificate
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(94)]
    string SslCipherList
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(96)]
    bool TlsVersion10Enabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(97)]
    bool TlsVersion11Enabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(98)]
    bool TlsVersion12Enabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(99)]
    int CrashSimulationMode { get; set; }

    [DispId(100)]
    string IMAPMasterUser
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(101)]
    bool IMAPSASLPlainEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(102)]
    bool IMAPSASLInitialResponseEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(103)]
    bool TlsVersion13Enabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(104)]
    bool IPv6PreferredEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(105)]
    bool TlsOptionPreferServerCiphersEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(106)]
    bool TlsOptionPrioritizeChaChaEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(107)]
    bool RewriteEnvelopeFromWhenForwarding
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }
}

[ComVisible(true)]
[Guid("FDF084A7-82DE-4EBE-8455-E506ACE01D63")]
[ProgId("hMailServer.Settings.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceSettings))]
public sealed class Settings : SettingsComAdapter, ISettingsAuthorizationBoundary
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private readonly bool _authorized;

    public Settings()
    {
    }

    private Settings(bool authorized)
    {
        _authorized = authorized;
    }

    public override IInterfaceMessageIndexing MessageIndexing
    {
        get
        {
            EnsureAuthorized();
            return MessageIndexingRuntimeHost.CreateAuthorizedAdapter();
        }
    }

    public override IInterfaceIMAPFolders PublicFolders
    {
        get
        {
            EnsureAuthorized();
            return ImapFolderAdministrationRuntimeHost.CreateAuthorizedAdapter(accountId: 0);
        }
    }

    public override IInterfaceSecurityRanges SecurityRanges
    {
        get
        {
            EnsureAuthorized();
            return SecurityRangeAdministrationRuntimeHost.CreateAuthorizedAdapter();
        }
    }

    public override IInterfaceRoutes Routes
    {
        get
        {
            EnsureAuthorized();
            return RouteAdministrationRuntimeHost.CreateAuthorizedAdapter();
        }
    }

    public override IInterfaceTCPIPPorts TCPIPPorts
    {
        get
        {
            EnsureAuthorized();
            return TcpIpPortAdministrationRuntimeHost.CreateAuthorizedAdapter();
        }
    }

    public override IInterfaceSSLCertificates SSLCertificates
    {
        get
        {
            EnsureAuthorized();
            return SslCertificateAdministrationRuntimeHost.CreateAuthorizedAdapter();
        }
    }

    public override IInterfaceIncomingRelays IncomingRelays
    {
        get
        {
            EnsureAuthorized();
            return IncomingRelayAdministrationRuntimeHost.CreateAuthorizedAdapter();
        }
    }

    public override IInterfaceGroups Groups
    {
        get
        {
            EnsureAuthorized();
            return GroupAdministrationRuntimeHost.CreateAuthorizedAdapter();
        }
    }

    internal static Settings CreateAuthorized() => new(authorized: true);

    void ISettingsAuthorizationBoundary.EnsureAuthorized() => EnsureAuthorized();

    private void EnsureAuthorized()
    {
        if (!_authorized)
        {
            throw new COMException("Settings access requires an authenticated server administrator.", EAccessDenied);
        }
    }
}

[ComVisible(false)]
public abstract class SettingsComAdapter : IInterfaceSettings
{
    public int MaxSMTPConnections { get => Unavailable<int>(); set => Unavailable(); }
    public int MaxPOP3Connections { get => Unavailable<int>(); set => Unavailable(); }
    public string MirrorEMailAddress { get => Unavailable<string>(); set => Unavailable(); }
    public bool AllowSMTPAuthPlain { get => Unavailable<bool>(); set => Unavailable(); }
    public bool DenyMailFromNull { get => Unavailable<bool>(); set => Unavailable(); }
    public IInterfaceLogging Logging => Unavailable<IInterfaceLogging>();
    public virtual IInterfaceSecurityRanges SecurityRanges => Unavailable<IInterfaceSecurityRanges>();
    public int SMTPNoOfTries { get => Unavailable<int>(); set => Unavailable(); }
    public int SMTPMinutesBetweenTry { get => Unavailable<int>(); set => Unavailable(); }
    public string SMTPRelayer { get => Unavailable<string>(); set => Unavailable(); }
    public string WelcomeSMTP { get => Unavailable<string>(); set => Unavailable(); }
    public string WelcomePOP3 { get => Unavailable<string>(); set => Unavailable(); }
    public string WelcomeIMAP { get => Unavailable<string>(); set => Unavailable(); }
    public bool ServiceSMTP { get => Unavailable<bool>(); set => Unavailable(); }
    public bool ServicePOP3 { get => Unavailable<bool>(); set => Unavailable(); }
    public bool ServiceIMAP { get => Unavailable<bool>(); set => Unavailable(); }
    public int MaxDeliveryThreads { get => Unavailable<int>(); set => Unavailable(); }
    public IInterfaceAntiVirus AntiVirus => Unavailable<IInterfaceAntiVirus>();
    public virtual IInterfaceRoutes Routes => Unavailable<IInterfaceRoutes>();
    public string HostName { get => Unavailable<string>(); set => Unavailable(); }
    public bool SMTPRelayerRequiresAuthentication { get => Unavailable<bool>(); set => Unavailable(); }
    public string SMTPRelayerUsername { get => Unavailable<string>(); set => Unavailable(); }
    public void SetSMTPRelayerPassword(string newVal) => Unavailable();
    public int SMTPRelayerPort { get => Unavailable<int>(); set => Unavailable(); }
    public string UserInterfaceLanguage { get => Unavailable<string>(); set => Unavailable(); }
    public IInterfaceScripting Scripting => Unavailable<IInterfaceScripting>();
    public int MaxMessageSize { get => Unavailable<int>(); set => Unavailable(); }
    public IInterfaceCache Cache => Unavailable<IInterfaceCache>();
    public int RuleLoopLimit { get => Unavailable<int>(); set => Unavailable(); }
    public IInterfaceBackupSettings Backup => Unavailable<IInterfaceBackupSettings>();
    public string DefaultDomain { get => Unavailable<string>(); set => Unavailable(); }
    public string SMTPDeliveryBindToIP { get => Unavailable<string>(); set => Unavailable(); }
    public int MaxIMAPConnections { get => Unavailable<int>(); set => Unavailable(); }
    public bool IMAPSortEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public bool IMAPQuotaEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public bool IMAPIdleEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public int WorkerThreadPriority { get => Unavailable<int>(); set => Unavailable(); }
    public int TCPIPThreads { get => Unavailable<int>(); set => Unavailable(); }
    public bool AllowIncorrectLineEndings { get => Unavailable<bool>(); set => Unavailable(); }
    public int MaxSMTPRecipientsInBatch { get => Unavailable<int>(); set => Unavailable(); }
    public IInterfaceAntiSpam AntiSpam => Unavailable<IInterfaceAntiSpam>();
    public bool DisconnectInvalidClients { get => Unavailable<bool>(); set => Unavailable(); }
    public int MaxNumberOfInvalidCommands { get => Unavailable<int>(); set => Unavailable(); }
    public IInterfaceServerMessages ServerMessages => Unavailable<IInterfaceServerMessages>();
    public virtual IInterfaceTCPIPPorts TCPIPPorts => Unavailable<IInterfaceTCPIPPorts>();
    public bool SMTPRelayerUseSSL { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual IInterfaceSSLCertificates SSLCertificates => Unavailable<IInterfaceSSLCertificates>();
    public bool AddDeliveredToHeader { get => Unavailable<bool>(); set => Unavailable(); }
    public string IMAPPublicFolderName { get => Unavailable<string>(); set => Unavailable(); }
    public bool IMAPACLEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public void SetAdministratorPassword(string newVal) => Unavailable();
    public IInterfaceDirectories Directories => Unavailable<IInterfaceDirectories>();
    public virtual IInterfaceIMAPFolders PublicFolders => Unavailable<IInterfaceIMAPFolders>();
    public string PublicFolderDiskName => Unavailable<string>();
    public virtual IInterfaceGroups Groups => Unavailable<IInterfaceGroups>();
    public virtual IInterfaceIncomingRelays IncomingRelays => Unavailable<IInterfaceIncomingRelays>();
    public bool AutoBanOnLogonFailure { get => Unavailable<bool>(); set => Unavailable(); }
    public int MaxInvalidLogonAttempts { get => Unavailable<int>(); set => Unavailable(); }
    public int MaxInvalidLogonAttemptsWithin { get => Unavailable<int>(); set => Unavailable(); }
    public int AutoBanMinutes { get => Unavailable<int>(); set => Unavailable(); }
    public void ClearLogonFailureList() => Unavailable();
    public string IMAPHierarchyDelimiter { get => Unavailable<string>(); set => Unavailable(); }
    public int MaxAsynchronousThreads { get => Unavailable<int>(); set => Unavailable(); }
    public virtual IInterfaceMessageIndexing MessageIndexing => Unavailable<IInterfaceMessageIndexing>();
    public int MaxNumberOfMXHosts { get => Unavailable<int>(); set => Unavailable(); }
    public ComConnectionSecurity SMTPRelayerConnectionSecurity { get => Unavailable<ComConnectionSecurity>(); set => Unavailable(); }
    public ComConnectionSecurity SMTPConnectionSecurity { get => Unavailable<ComConnectionSecurity>(); set => Unavailable(); }
    public bool VerifyRemoteSslCertificate { get => Unavailable<bool>(); set => Unavailable(); }
    public string SslCipherList { get => Unavailable<string>(); set => Unavailable(); }
    public bool TlsVersion10Enabled { get => Unavailable<bool>(); set => Unavailable(); }
    public bool TlsVersion11Enabled { get => Unavailable<bool>(); set => Unavailable(); }
    public bool TlsVersion12Enabled { get => Unavailable<bool>(); set => Unavailable(); }
    public int CrashSimulationMode { get => Unavailable<int>(); set => Unavailable(); }
    public string IMAPMasterUser { get => Unavailable<string>(); set => Unavailable(); }
    public bool IMAPSASLPlainEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public bool IMAPSASLInitialResponseEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public bool TlsVersion13Enabled { get => Unavailable<bool>(); set => Unavailable(); }
    public bool IPv6PreferredEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public bool TlsOptionPreferServerCiphersEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public bool TlsOptionPrioritizeChaChaEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public bool RewriteEnvelopeFromWhenForwarding { get => Unavailable<bool>(); set => Unavailable(); }

    private T Unavailable<T>() => SettingsComAuthorization.Unavailable<T>(this);

    private void Unavailable() => SettingsComAuthorization.Unavailable(this);
}

[ComVisible(false)]
internal interface ISettingsAuthorizationBoundary
{
    void EnsureAuthorized();
}

[ComVisible(false)]
internal static class SettingsComAuthorization
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    internal static T Unavailable<T>(IInterfaceSettings settings)
    {
        EnsureAuthorized(settings);
        throw new COMException("This Settings member is not implemented by the .NET 10 rewrite yet.", ENotImplemented);
    }

    internal static void Unavailable(IInterfaceSettings settings)
    {
        EnsureAuthorized(settings);
        throw new COMException("This Settings member is not implemented by the .NET 10 rewrite yet.", ENotImplemented);
    }

    private static void EnsureAuthorized(IInterfaceSettings settings)
    {
        if (settings is not ISettingsAuthorizationBoundary boundary)
        {
            throw new COMException("Settings access requires an authenticated server administrator.", EAccessDenied);
        }

        boundary.EnsureAuthorized();
    }
}
