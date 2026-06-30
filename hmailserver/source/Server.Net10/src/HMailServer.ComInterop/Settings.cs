using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

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
    private readonly SettingsAdministrationSnapshot? _administrationSnapshot;

    public Settings()
    {
    }

    private Settings(bool authorized, SettingsAdministrationSnapshot? administrationSnapshot = null)
    {
        _authorized = authorized;
        _administrationSnapshot = administrationSnapshot;
    }

    public override int MaxSMTPConnections
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.MaxSMTPConnections
                : _administrationSnapshot.MaxSmtpConnections;
        }
        set => base.MaxSMTPConnections = value;
    }

    public override int MaxPOP3Connections
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.MaxPOP3Connections
                : _administrationSnapshot.MaxPop3Connections;
        }
        set => base.MaxPOP3Connections = value;
    }

    public override int SMTPNoOfTries
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.SMTPNoOfTries
                : _administrationSnapshot.SmtpNoOfTries;
        }
        set => base.SMTPNoOfTries = value;
    }

    public override int SMTPMinutesBetweenTry
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.SMTPMinutesBetweenTry
                : _administrationSnapshot.SmtpMinutesBetweenTry;
        }
        set => base.SMTPMinutesBetweenTry = value;
    }

    public override string WelcomeSMTP
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.WelcomeSMTP
                : _administrationSnapshot.WelcomeSmtp;
        }
        set => base.WelcomeSMTP = value;
    }

    public override string WelcomePOP3
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.WelcomePOP3
                : _administrationSnapshot.WelcomePop3;
        }
        set => base.WelcomePOP3 = value;
    }

    public override string WelcomeIMAP
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.WelcomeIMAP
                : _administrationSnapshot.WelcomeImap;
        }
        set => base.WelcomeIMAP = value;
    }

    public override bool ServiceSMTP
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.ServiceSMTP
                : _administrationSnapshot.ServiceSmtp;
        }
        set => base.ServiceSMTP = value;
    }

    public override bool ServicePOP3
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.ServicePOP3
                : _administrationSnapshot.ServicePop3;
        }
        set => base.ServicePOP3 = value;
    }

    public override bool ServiceIMAP
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.ServiceIMAP
                : _administrationSnapshot.ServiceImap;
        }
        set => base.ServiceIMAP = value;
    }

    public override string HostName
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.HostName
                : _administrationSnapshot.HostName;
        }
        set => base.HostName = value;
    }

    public override int MaxDeliveryThreads
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.MaxDeliveryThreads
                : _administrationSnapshot.MaxDeliveryThreads;
        }
        set => base.MaxDeliveryThreads = value;
    }

    public override int MaxIMAPConnections
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.MaxIMAPConnections
                : _administrationSnapshot.MaxImapConnections;
        }
        set => base.MaxIMAPConnections = value;
    }

    public override bool IMAPSortEnabled
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.IMAPSortEnabled
                : _administrationSnapshot.ImapSortEnabled;
        }
        set => base.IMAPSortEnabled = value;
    }

    public override bool IMAPQuotaEnabled
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.IMAPQuotaEnabled
                : _administrationSnapshot.ImapQuotaEnabled;
        }
        set => base.IMAPQuotaEnabled = value;
    }

    public override bool IMAPIdleEnabled
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.IMAPIdleEnabled
                : _administrationSnapshot.ImapIdleEnabled;
        }
        set => base.IMAPIdleEnabled = value;
    }

    public override int MaxMessageSize
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.MaxMessageSize
                : _administrationSnapshot.MaxMessageSize;
        }
        set => base.MaxMessageSize = value;
    }

    public override int MaxSMTPRecipientsInBatch
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.MaxSMTPRecipientsInBatch
                : _administrationSnapshot.MaxSmtpRecipientsInBatch;
        }
        set => base.MaxSMTPRecipientsInBatch = value;
    }

    public override bool DisconnectInvalidClients
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.DisconnectInvalidClients
                : _administrationSnapshot.DisconnectInvalidClients;
        }
        set => base.DisconnectInvalidClients = value;
    }

    public override int MaxNumberOfInvalidCommands
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.MaxNumberOfInvalidCommands
                : _administrationSnapshot.MaxNumberOfInvalidCommands;
        }
        set => base.MaxNumberOfInvalidCommands = value;
    }

    public override bool IMAPACLEnabled
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.IMAPACLEnabled
                : _administrationSnapshot.ImapAclEnabled;
        }
        set => base.IMAPACLEnabled = value;
    }

    public override bool IMAPSASLPlainEnabled
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.IMAPSASLPlainEnabled
                : _administrationSnapshot.ImapSaslPlainEnabled;
        }
        set => base.IMAPSASLPlainEnabled = value;
    }

    public override bool IMAPSASLInitialResponseEnabled
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.IMAPSASLInitialResponseEnabled
                : _administrationSnapshot.ImapSaslInitialResponseEnabled;
        }
        set => base.IMAPSASLInitialResponseEnabled = value;
    }

    public override string IMAPPublicFolderName
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.IMAPPublicFolderName
                : _administrationSnapshot.ImapPublicFolderName;
        }
        set => base.IMAPPublicFolderName = value;
    }

    public override string IMAPHierarchyDelimiter
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.IMAPHierarchyDelimiter
                : _administrationSnapshot.ImapHierarchyDelimiter;
        }
        set => base.IMAPHierarchyDelimiter = value;
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

    public override IInterfaceServerMessages ServerMessages
    {
        get
        {
            EnsureAuthorized();
            return ServerMessageAdministrationRuntimeHost.CreateAuthorizedAdapter();
        }
    }

    public override IInterfaceDirectories Directories
    {
        get
        {
            EnsureAuthorized();
            return DirectoryAdministrationRuntimeHost.CreateAuthorizedAdapter();
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

    internal static Settings CreateAuthorized(SettingsAdministrationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new Settings(authorized: true, administrationSnapshot: snapshot);
    }

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
    public virtual int MaxSMTPConnections { get => Unavailable<int>(); set => Unavailable(); }
    public virtual int MaxPOP3Connections { get => Unavailable<int>(); set => Unavailable(); }
    public string MirrorEMailAddress { get => Unavailable<string>(); set => Unavailable(); }
    public bool AllowSMTPAuthPlain { get => Unavailable<bool>(); set => Unavailable(); }
    public bool DenyMailFromNull { get => Unavailable<bool>(); set => Unavailable(); }
    public IInterfaceLogging Logging => Unavailable<IInterfaceLogging>();
    public virtual IInterfaceSecurityRanges SecurityRanges => Unavailable<IInterfaceSecurityRanges>();
    public virtual int SMTPNoOfTries { get => Unavailable<int>(); set => Unavailable(); }
    public virtual int SMTPMinutesBetweenTry { get => Unavailable<int>(); set => Unavailable(); }
    public string SMTPRelayer { get => Unavailable<string>(); set => Unavailable(); }
    public virtual string WelcomeSMTP { get => Unavailable<string>(); set => Unavailable(); }
    public virtual string WelcomePOP3 { get => Unavailable<string>(); set => Unavailable(); }
    public virtual string WelcomeIMAP { get => Unavailable<string>(); set => Unavailable(); }
    public virtual bool ServiceSMTP { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool ServicePOP3 { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool ServiceIMAP { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual int MaxDeliveryThreads { get => Unavailable<int>(); set => Unavailable(); }
    public IInterfaceAntiVirus AntiVirus => Unavailable<IInterfaceAntiVirus>();
    public virtual IInterfaceRoutes Routes => Unavailable<IInterfaceRoutes>();
    public virtual string HostName { get => Unavailable<string>(); set => Unavailable(); }
    public bool SMTPRelayerRequiresAuthentication { get => Unavailable<bool>(); set => Unavailable(); }
    public string SMTPRelayerUsername { get => Unavailable<string>(); set => Unavailable(); }
    public void SetSMTPRelayerPassword(string newVal) => Unavailable();
    public int SMTPRelayerPort { get => Unavailable<int>(); set => Unavailable(); }
    public string UserInterfaceLanguage { get => Unavailable<string>(); set => Unavailable(); }
    public IInterfaceScripting Scripting => Unavailable<IInterfaceScripting>();
    public virtual int MaxMessageSize { get => Unavailable<int>(); set => Unavailable(); }
    public IInterfaceCache Cache => Unavailable<IInterfaceCache>();
    public int RuleLoopLimit { get => Unavailable<int>(); set => Unavailable(); }
    public IInterfaceBackupSettings Backup => Unavailable<IInterfaceBackupSettings>();
    public string DefaultDomain { get => Unavailable<string>(); set => Unavailable(); }
    public string SMTPDeliveryBindToIP { get => Unavailable<string>(); set => Unavailable(); }
    public virtual int MaxIMAPConnections { get => Unavailable<int>(); set => Unavailable(); }
    public virtual bool IMAPSortEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool IMAPQuotaEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool IMAPIdleEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public int WorkerThreadPriority { get => Unavailable<int>(); set => Unavailable(); }
    public int TCPIPThreads { get => Unavailable<int>(); set => Unavailable(); }
    public bool AllowIncorrectLineEndings { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual int MaxSMTPRecipientsInBatch { get => Unavailable<int>(); set => Unavailable(); }
    public IInterfaceAntiSpam AntiSpam => Unavailable<IInterfaceAntiSpam>();
    public virtual bool DisconnectInvalidClients { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual int MaxNumberOfInvalidCommands { get => Unavailable<int>(); set => Unavailable(); }
    public virtual IInterfaceServerMessages ServerMessages => Unavailable<IInterfaceServerMessages>();
    public virtual IInterfaceTCPIPPorts TCPIPPorts => Unavailable<IInterfaceTCPIPPorts>();
    public bool SMTPRelayerUseSSL { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual IInterfaceSSLCertificates SSLCertificates => Unavailable<IInterfaceSSLCertificates>();
    public bool AddDeliveredToHeader { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual string IMAPPublicFolderName { get => Unavailable<string>(); set => Unavailable(); }
    public virtual bool IMAPACLEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public void SetAdministratorPassword(string newVal) => Unavailable();
    public virtual IInterfaceDirectories Directories => Unavailable<IInterfaceDirectories>();
    public virtual IInterfaceIMAPFolders PublicFolders => Unavailable<IInterfaceIMAPFolders>();
    public string PublicFolderDiskName => Unavailable<string>();
    public virtual IInterfaceGroups Groups => Unavailable<IInterfaceGroups>();
    public virtual IInterfaceIncomingRelays IncomingRelays => Unavailable<IInterfaceIncomingRelays>();
    public bool AutoBanOnLogonFailure { get => Unavailable<bool>(); set => Unavailable(); }
    public int MaxInvalidLogonAttempts { get => Unavailable<int>(); set => Unavailable(); }
    public int MaxInvalidLogonAttemptsWithin { get => Unavailable<int>(); set => Unavailable(); }
    public int AutoBanMinutes { get => Unavailable<int>(); set => Unavailable(); }
    public void ClearLogonFailureList() => Unavailable();
    public virtual string IMAPHierarchyDelimiter { get => Unavailable<string>(); set => Unavailable(); }
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
    public virtual bool IMAPSASLPlainEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool IMAPSASLInitialResponseEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public bool TlsVersion13Enabled { get => Unavailable<bool>(); set => Unavailable(); }
    public bool IPv6PreferredEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public bool TlsOptionPreferServerCiphersEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public bool TlsOptionPrioritizeChaChaEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public bool RewriteEnvelopeFromWhenForwarding { get => Unavailable<bool>(); set => Unavailable(); }

    private T Unavailable<T>() => SettingsComAuthorization.Unavailable<T>(this);

    private void Unavailable() => SettingsComAuthorization.Unavailable(this);
}

[ComVisible(false)]
public static class SettingsAdministrationRuntimeHost
{
    private static ISettingsAdministrationStore? _store;

    public static void Configure(ISettingsAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static Settings CreateAuthorizedAdapter()
    {
        var store = Volatile.Read(ref _store);
        if (store is null)
        {
            return Settings.CreateAuthorized();
        }

        var snapshot = store
            .GetSettingsAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return Settings.CreateAuthorized(snapshot);
    }
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
