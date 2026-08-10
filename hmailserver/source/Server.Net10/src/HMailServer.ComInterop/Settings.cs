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
    private const int EFail = unchecked((int)0x80004005);
    private const int TlsVersion10Flag = 2;
    private const int TlsVersion11Flag = 4;
    private const int TlsVersion12Flag = 8;
    private const int TlsVersion13Flag = 16;
    private const int TlsOptionPreferServerCiphersFlag = 2;
    private const int TlsOptionPrioritizeChaChaFlag = 4;
    private readonly bool _authorized;
    private SettingsAdministrationSnapshot? _administrationSnapshot;
    private readonly SettingsRuntimeConfiguration _runtimeConfiguration = new();
    private readonly Func<bool>? _isServerAdministrator;
    private readonly ISettingsAdministrationMutationStore? _settingsMutationStore;

    public Settings()
    {
    }

    private Settings(
        bool authorized,
        SettingsAdministrationSnapshot? administrationSnapshot = null,
        SettingsRuntimeConfiguration? runtimeConfiguration = null,
        Func<bool>? isServerAdministrator = null,
        ISettingsAdministrationMutationStore? settingsMutationStore = null)
    {
        _authorized = authorized;
        _administrationSnapshot = administrationSnapshot;
        _runtimeConfiguration = runtimeConfiguration ?? new SettingsRuntimeConfiguration();
        _isServerAdministrator = isServerAdministrator;
        _settingsMutationStore = settingsMutationStore;
    }

    public override string UserInterfaceLanguage
    {
        get
        {
            EnsureAuthorized();
            return _runtimeConfiguration.UserInterfaceLanguage;
        }
        set => base.UserInterfaceLanguage = value;
    }

    public override bool RewriteEnvelopeFromWhenForwarding
    {
        get
        {
            EnsureAuthorized();
            return _runtimeConfiguration.RewriteEnvelopeFromWhenForwarding;
        }
        set => base.RewriteEnvelopeFromWhenForwarding = value;
    }

    public override int CrashSimulationMode
    {
        get
        {
            EnsureAuthorized();
            return _runtimeConfiguration.CrashSimulationMode;
        }
        set => base.CrashSimulationMode = value;
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
        set
        {
            EnsureAuthorized();
            EnsureServerAdministrator();

            if (_settingsMutationStore is null)
            {
                base.MaxSMTPConnections = value;
                return;
            }

            if (!_settingsMutationStore
                .UpdateMaxSmtpConnectionsAsync(value, CancellationToken.None)
                .GetAwaiter()
                .GetResult())
            {
                throw new COMException(
                    "The maximum SMTP connections update did not affect the existing settings row.",
                    EFail);
            }

            if (_administrationSnapshot is not null)
            {
                _administrationSnapshot = _administrationSnapshot with
                {
                    MaxSmtpConnections = value
                };
            }
        }
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
        set
        {
            EnsureAuthorized();
            EnsureServerAdministrator();

            if (_settingsMutationStore is null)
            {
                base.MaxPOP3Connections = value;
                return;
            }

            if (!_settingsMutationStore
                .UpdateMaxPop3ConnectionsAsync(value, CancellationToken.None)
                .GetAwaiter()
                .GetResult())
            {
                throw new COMException(
                    "The maximum POP3 connections update did not affect the existing settings row.",
                    EFail);
            }

            if (_administrationSnapshot is not null)
            {
                _administrationSnapshot = _administrationSnapshot with
                {
                    MaxPop3Connections = value
                };
            }
        }
    }

    public override string MirrorEMailAddress
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.MirrorEMailAddress
                : _administrationSnapshot.MirrorEmailAddress;
        }
        set
        {
            EnsureAuthorized();
            EnsureServerAdministrator();
            if (_settingsMutationStore is null)
            {
                base.MirrorEMailAddress = value;
                return;
            }

            var persisted = _settingsMutationStore
                .UpdateMirrorEmailAddressAsync(value, CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
            if (!persisted)
            {
                throw new COMException(
                    "The mirror email address update did not affect the existing settings row.",
                    EFail);
            }

            if (_administrationSnapshot is not null)
            {
                _administrationSnapshot = _administrationSnapshot with { MirrorEmailAddress = value };
            }
        }
    }

    public override bool AllowSMTPAuthPlain
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.AllowSMTPAuthPlain
                : _administrationSnapshot.AllowSmtpAuthPlain;
        }
        set => base.AllowSMTPAuthPlain = value;
    }

    public override bool DenyMailFromNull
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.DenyMailFromNull
                : !_administrationSnapshot.AllowMailFromNull;
        }
        set => base.DenyMailFromNull = value;
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

    public override string SMTPRelayer
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.SMTPRelayer
                : _administrationSnapshot.SmtpRelayer;
        }
        set => base.SMTPRelayer = value;
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
        set
        {
            EnsureAuthorized();
            EnsureServerAdministrator();

            if (_settingsMutationStore is null)
            {
                base.WelcomePOP3 = value;
                return;
            }

            if (!_settingsMutationStore
                .UpdateWelcomePop3Async(value, CancellationToken.None)
                .GetAwaiter()
                .GetResult())
            {
                throw new COMException(
                    "The POP3 welcome message update did not affect the existing settings row.",
                    EFail);
            }

            if (_administrationSnapshot is not null)
            {
                _administrationSnapshot = _administrationSnapshot with
                {
                    WelcomePop3 = value
                };
            }
        }
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

    public override bool SMTPRelayerRequiresAuthentication
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.SMTPRelayerRequiresAuthentication
                : _administrationSnapshot.SmtpRelayerRequiresAuthentication;
        }
        set => base.SMTPRelayerRequiresAuthentication = value;
    }

    public override string SMTPRelayerUsername
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.SMTPRelayerUsername
                : _administrationSnapshot.SmtpRelayerUsername;
        }
        set => base.SMTPRelayerUsername = value;
    }

    public override int SMTPRelayerPort
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.SMTPRelayerPort
                : _administrationSnapshot.SmtpRelayerPort;
        }
        set => base.SMTPRelayerPort = value;
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

    public override int WorkerThreadPriority
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.WorkerThreadPriority
                : _administrationSnapshot.WorkerThreadPriority;
        }
        set
        {
            EnsureAuthorized();
            EnsureServerAdministrator();

            if (_settingsMutationStore is null)
            {
                base.WorkerThreadPriority = value;
                return;
            }

            if (!_settingsMutationStore
                .UpdateWorkerThreadPriorityAsync(value, CancellationToken.None)
                .GetAwaiter()
                .GetResult())
            {
                throw new COMException(
                    "The worker thread priority update did not affect the existing settings row.",
                    EFail);
            }

            if (_administrationSnapshot is not null)
            {
                _administrationSnapshot = _administrationSnapshot with
                {
                    WorkerThreadPriority = value
                };
            }
        }
    }

    public override int TCPIPThreads
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.TCPIPThreads
                : _administrationSnapshot.TcpIpThreads;
        }
        set => base.TCPIPThreads = value;
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

    public override int RuleLoopLimit
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.RuleLoopLimit
                : _administrationSnapshot.RuleLoopLimit;
        }
        set => base.RuleLoopLimit = value;
    }

    public override string DefaultDomain
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.DefaultDomain
                : _administrationSnapshot.DefaultDomain;
        }
        set
        {
            EnsureAuthorized();
            EnsureServerAdministrator();
            if (_settingsMutationStore is null)
            {
                base.DefaultDomain = value;
                return;
            }

            var persisted = _settingsMutationStore
                .UpdateDefaultDomainAsync(value, CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
            if (!persisted)
            {
                throw new COMException("The default domain update did not affect the existing settings row.", EFail);
            }

            if (_administrationSnapshot is not null)
            {
                _administrationSnapshot = _administrationSnapshot with { DefaultDomain = value };
            }
        }
    }

    public override string SMTPDeliveryBindToIP
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.SMTPDeliveryBindToIP
                : _administrationSnapshot.SmtpDeliveryBindToIp;
        }
        set => base.SMTPDeliveryBindToIP = value;
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

    public override bool AllowIncorrectLineEndings
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.AllowIncorrectLineEndings
                : _administrationSnapshot.AllowIncorrectLineEndings;
        }
        set => base.AllowIncorrectLineEndings = value;
    }

    public override bool AddDeliveredToHeader
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.AddDeliveredToHeader
                : _administrationSnapshot.AddDeliveredToHeader;
        }
        set => base.AddDeliveredToHeader = value;
    }

    public override IInterfaceMessageIndexing MessageIndexing
    {
        get
        {
            EnsureAuthorized();
            EnsureServerAdministrator();
            return MessageIndexingRuntimeHost.CreateAuthorizedAdapter(_isServerAdministrator);
        }
    }

    public override int MaxNumberOfMXHosts
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.MaxNumberOfMXHosts
                : _administrationSnapshot.MaxNumberOfMxHosts;
        }
        set => base.MaxNumberOfMXHosts = value;
    }

    public override ComConnectionSecurity SMTPRelayerConnectionSecurity
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.SMTPRelayerConnectionSecurity
                : (ComConnectionSecurity)_administrationSnapshot.SmtpRelayerConnectionSecurity;
        }
        set => base.SMTPRelayerConnectionSecurity = value;
    }

    public override bool SMTPRelayerUseSSL
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.SMTPRelayerUseSSL
                : _administrationSnapshot.SmtpRelayerConnectionSecurity == (int)ComConnectionSecurity.Tls;
        }
        set => base.SMTPRelayerUseSSL = value;
    }

    public override ComConnectionSecurity SMTPConnectionSecurity
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.SMTPConnectionSecurity
                : (ComConnectionSecurity)_administrationSnapshot.SmtpConnectionSecurity;
        }
        set => base.SMTPConnectionSecurity = value;
    }

    public override bool TlsVersion10Enabled
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.TlsVersion10Enabled
                : HasFlag(_administrationSnapshot.SslVersions, TlsVersion10Flag);
        }
        set => base.TlsVersion10Enabled = value;
    }

    public override bool TlsVersion11Enabled
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.TlsVersion11Enabled
                : HasFlag(_administrationSnapshot.SslVersions, TlsVersion11Flag);
        }
        set => base.TlsVersion11Enabled = value;
    }

    public override bool TlsVersion12Enabled
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.TlsVersion12Enabled
                : HasFlag(_administrationSnapshot.SslVersions, TlsVersion12Flag);
        }
        set => base.TlsVersion12Enabled = value;
    }

    public override bool TlsVersion13Enabled
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.TlsVersion13Enabled
                : HasFlag(_administrationSnapshot.SslVersions, TlsVersion13Flag);
        }
        set => base.TlsVersion13Enabled = value;
    }

    public override bool TlsOptionPreferServerCiphersEnabled
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.TlsOptionPreferServerCiphersEnabled
                : HasFlag(_administrationSnapshot.TlsOptions, TlsOptionPreferServerCiphersFlag);
        }
        set => base.TlsOptionPreferServerCiphersEnabled = value;
    }

    public override bool TlsOptionPrioritizeChaChaEnabled
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.TlsOptionPrioritizeChaChaEnabled
                : HasFlag(_administrationSnapshot.TlsOptions, TlsOptionPrioritizeChaChaFlag);
        }
        set => base.TlsOptionPrioritizeChaChaEnabled = value;
    }

    public override string IMAPMasterUser
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.IMAPMasterUser
                : _administrationSnapshot.ImapMasterUser;
        }
        set => base.IMAPMasterUser = value;
    }

    public override int MaxAsynchronousThreads
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.MaxAsynchronousThreads
                : _administrationSnapshot.MaxAsynchronousThreads;
        }
        set => base.MaxAsynchronousThreads = value;
    }

    public override IInterfaceLogging Logging
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.Logging
                : HMailServer.ComInterop.Logging.CreateAuthorized(
                    new LoggingAdministrationSnapshot(
                        _administrationSnapshot.LoggingMask,
                        _administrationSnapshot.LogDevice,
                        _administrationSnapshot.LogFormat,
                        _administrationSnapshot.AwStatsEnabled,
                        _runtimeConfiguration.LoggingDirectory),
                    _runtimeConfiguration.LoggingTimeProvider,
                    _runtimeConfiguration.LoggingLiveLogRuntime);
        }
    }

    public override IInterfaceScripting Scripting
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.Scripting
                : HMailServer.ComInterop.Scripting.CreateAuthorized(
                    new ScriptingAdministrationSnapshot(
                        _administrationSnapshot.UseScriptServer,
                        _administrationSnapshot.ScriptLanguage,
                        _runtimeConfiguration.ScriptingDirectory),
                    _runtimeConfiguration.ScriptSyntaxChecker,
                    _runtimeConfiguration.ScriptRuntimeReloader);
        }
    }

    public override IInterfaceBackupSettings Backup
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.Backup
                : HMailServer.ComInterop.BackupSettings.CreateAuthorized(
                    new BackupSettingsAdministrationSnapshot(
                        _administrationSnapshot.BackupDestination,
                        _administrationSnapshot.BackupOptions,
                        _runtimeConfiguration.LoggingDirectory));
        }
    }

    public override IInterfaceAntiVirus AntiVirus
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.AntiVirus
                : HMailServer.ComInterop.AntiVirus.CreateAuthorized(
                    new AntiVirusAdministrationSnapshot(
                        _administrationSnapshot.AntiVirusClamWinEnabled,
                        _administrationSnapshot.AntiVirusClamWinExecutable,
                        _administrationSnapshot.AntiVirusClamWinDatabase,
                        _administrationSnapshot.AntiVirusAction,
                        _administrationSnapshot.AntiVirusNotifyReceiver,
                        _administrationSnapshot.AntiVirusNotifySender,
                        _administrationSnapshot.AntiVirusCustomScannerEnabled,
                        _administrationSnapshot.AntiVirusCustomScannerExecutable,
                        _administrationSnapshot.AntiVirusCustomScannerReturnValue,
                        _administrationSnapshot.AntiVirusMaximumMessageSize,
                        _administrationSnapshot.AntiVirusEnableAttachmentBlocking,
                        _administrationSnapshot.AntiVirusClamAvEnabled,
                        _administrationSnapshot.AntiVirusClamAvHost,
                        _administrationSnapshot.AntiVirusClamAvPort),
                    clamAvScannerTestRuntime: _runtimeConfiguration.ClamAvScannerTestRuntime,
                    clamWinScannerTestRuntime: _runtimeConfiguration.ClamWinScannerTestRuntime,
                    customScannerTestRuntime: _runtimeConfiguration.CustomScannerTestRuntime,
                    isServerAdministrator: _isServerAdministrator);
        }
    }

    public override IInterfaceAntiSpam AntiSpam
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.AntiSpam
                : _isServerAdministrator is not null && !_isServerAdministrator()
                    ? HMailServer.ComInterop.AntiSpam.CreateDenied()
                : HMailServer.ComInterop.AntiSpam.CreateAuthorized(
                    new AntiSpamAdministrationSnapshot(
                        _administrationSnapshot.AntiSpamGreyListingEnabled,
                        _administrationSnapshot.AntiSpamGreyListingInitialDelay,
                        _administrationSnapshot.AntiSpamGreyListingInitialDelete,
                        _administrationSnapshot.AntiSpamGreyListingFinalDelete,
                        _administrationSnapshot.AntiSpamCheckHostInHelo,
                        _administrationSnapshot.AntiSpamCheckHostInHeloScore,
                        _administrationSnapshot.AntiSpamCheckPtr,
                        _administrationSnapshot.AntiSpamCheckPtrScore,
                        _administrationSnapshot.AntiSpamAddHeaderSpam,
                        _administrationSnapshot.AntiSpamAddHeaderReason,
                        _administrationSnapshot.AntiSpamPrependSubject,
                        _administrationSnapshot.AntiSpamPrependSubjectText,
                        _administrationSnapshot.AntiSpamSpamMarkThreshold,
                        _administrationSnapshot.AntiSpamSpamDeleteThreshold,
                        _administrationSnapshot.AntiSpamUseSpf,
                        _administrationSnapshot.AntiSpamUseSpfScore,
                        _administrationSnapshot.AntiSpamUseMxChecks,
                        _administrationSnapshot.AntiSpamUseMxChecksScore,
                        _administrationSnapshot.AntiSpamSpamAssassinEnabled,
                        _administrationSnapshot.AntiSpamSpamAssassinScore,
                        _administrationSnapshot.AntiSpamSpamAssassinMergeScore,
                        _administrationSnapshot.AntiSpamSpamAssassinHost,
                        _administrationSnapshot.AntiSpamSpamAssassinPort,
                        _administrationSnapshot.AntiSpamMaximumMessageSize,
                        _administrationSnapshot.AntiSpamDkimVerificationEnabled,
                        _administrationSnapshot.AntiSpamDkimVerificationFailureScore,
                        _administrationSnapshot.AntiSpamBypassGreylistingOnSpfSuccess,
                        _administrationSnapshot.AntiSpamBypassGreylistingOnMailFromMx),
                    _runtimeConfiguration.DkimVerificationRuntime,
                    _runtimeConfiguration.GreyListingTripletAdministrationStore,
                    _runtimeConfiguration.SpamAssassinConnectionTestRuntime,
                    isServerAdministrator: _isServerAdministrator);
        }
    }

    public override bool VerifyRemoteSslCertificate
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.VerifyRemoteSslCertificate
                : _administrationSnapshot.VerifyRemoteSslCertificate;
        }
        set => base.VerifyRemoteSslCertificate = value;
    }

    public override string SslCipherList
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.SslCipherList
                : _administrationSnapshot.SslCipherList;
        }
        set => base.SslCipherList = value;
    }

    public override bool IPv6PreferredEnabled
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.IPv6PreferredEnabled
                : _administrationSnapshot.Ipv6PreferredEnabled;
        }
        set => base.IPv6PreferredEnabled = value;
    }

    public override bool AutoBanOnLogonFailure
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.AutoBanOnLogonFailure
                : _administrationSnapshot.AutoBanOnLogonFailure;
        }
        set => base.AutoBanOnLogonFailure = value;
    }

    public override int MaxInvalidLogonAttempts
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.MaxInvalidLogonAttempts
                : _administrationSnapshot.MaxInvalidLogonAttempts;
        }
        set => base.MaxInvalidLogonAttempts = value;
    }

    public override int MaxInvalidLogonAttemptsWithin
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.MaxInvalidLogonAttemptsWithin
                : _administrationSnapshot.MaxInvalidLogonAttemptsWithin;
        }
        set => base.MaxInvalidLogonAttemptsWithin = value;
    }

    public override int AutoBanMinutes
    {
        get
        {
            EnsureAuthorized();
            return _administrationSnapshot is null
                ? base.AutoBanMinutes
                : _administrationSnapshot.AutoBanMinutes;
        }
        set => base.AutoBanMinutes = value;
    }

    public override void ClearLogonFailureList()
    {
        EnsureAuthorized();
        if (_runtimeConfiguration.LogonFailureAdministrationStore is null)
        {
            base.ClearLogonFailureList();
            return;
        }

        _runtimeConfiguration.LogonFailureAdministrationStore
            .ClearLegacyListAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }

    public override IInterfaceIMAPFolders PublicFolders
    {
        get
        {
            EnsureAuthorized();
            return ImapFolderAdministrationRuntimeHost.CreateAuthorizedAdapter(
                accountId: 0,
                isAuthenticated: _isServerAdministrator);
        }
    }

    public override IInterfaceSecurityRanges SecurityRanges
    {
        get
        {
            EnsureAuthorized();
            EnsureServerAdministrator();
            return SecurityRangeAdministrationRuntimeHost.CreateAuthorizedAdapter(_isServerAdministrator);
        }
    }

    public override IInterfaceRoutes Routes
    {
        get
        {
            EnsureAuthorized();
            EnsureServerAdministrator();
            return RouteAdministrationRuntimeHost.CreateAuthorizedAdapter(_isServerAdministrator);
        }
    }

    public override IInterfaceServerMessages ServerMessages
    {
        get
        {
            EnsureAuthorized();
            EnsureServerAdministrator();
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
            EnsureServerAdministrator();
            return TcpIpPortAdministrationRuntimeHost.CreateAuthorizedAdapter(_isServerAdministrator);
        }
    }

    public override IInterfaceSSLCertificates SSLCertificates
    {
        get
        {
            EnsureAuthorized();
            EnsureServerAdministrator();
            return SslCertificateAdministrationRuntimeHost.CreateAuthorizedAdapter(_isServerAdministrator);
        }
    }

    public override IInterfaceIncomingRelays IncomingRelays
    {
        get
        {
            EnsureAuthorized();
            EnsureServerAdministrator();
            return IncomingRelayAdministrationRuntimeHost.CreateAuthorizedAdapter(_isServerAdministrator);
        }
    }

    public override IInterfaceGroups Groups
    {
        get
        {
            EnsureAuthorized();
            EnsureServerAdministrator();
            return GroupAdministrationRuntimeHost.CreateAuthorizedAdapter(_isServerAdministrator);
        }
    }

    public override IInterfaceCache Cache
    {
        get
        {
            EnsureAuthorized();
            EnsureServerAdministrator();
            return _administrationSnapshot is null
                ? base.Cache
                : CacheAdministrationRuntimeHost.CreateAuthorizedAdapter(
                    new CacheAdministrationSnapshot(
                        _administrationSnapshot.CacheEnabled,
                        _administrationSnapshot.DomainCacheTtl,
                        _administrationSnapshot.AccountCacheTtl,
                        _administrationSnapshot.AliasCacheTtl,
                        _administrationSnapshot.DistributionListCacheTtl),
                    _isServerAdministrator);
        }
    }

    public override string PublicFolderDiskName
    {
        get
        {
            EnsureAuthorized();
            return "#Public";
        }
    }

    private static bool HasFlag(int value, int flag) => (value & flag) != 0;

    internal static Settings CreateAuthorized(Func<bool>? isServerAdministrator = null) =>
        new(authorized: true, isServerAdministrator: isServerAdministrator);

    internal static Settings CreateAuthorized(
        SettingsAdministrationSnapshot snapshot,
        SettingsRuntimeConfiguration? runtimeConfiguration = null,
        Func<bool>? isServerAdministrator = null,
        ISettingsAdministrationMutationStore? settingsMutationStore = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new Settings(
            authorized: true,
            administrationSnapshot: snapshot,
            runtimeConfiguration: runtimeConfiguration,
            isServerAdministrator: isServerAdministrator,
            settingsMutationStore: settingsMutationStore);
    }

    void ISettingsAuthorizationBoundary.EnsureAuthorized() => EnsureAuthorized();

    private void EnsureAuthorized()
    {
        if (!_authorized)
        {
            throw new COMException("Settings access requires an authenticated server administrator.", EAccessDenied);
        }
    }

    private void EnsureServerAdministrator()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
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
    public virtual string MirrorEMailAddress { get => Unavailable<string>(); set => Unavailable(); }
    public virtual bool AllowSMTPAuthPlain { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool DenyMailFromNull { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual IInterfaceLogging Logging => Unavailable<IInterfaceLogging>();
    public virtual IInterfaceSecurityRanges SecurityRanges => Unavailable<IInterfaceSecurityRanges>();
    public virtual int SMTPNoOfTries { get => Unavailable<int>(); set => Unavailable(); }
    public virtual int SMTPMinutesBetweenTry { get => Unavailable<int>(); set => Unavailable(); }
    public virtual string SMTPRelayer { get => Unavailable<string>(); set => Unavailable(); }
    public virtual string WelcomeSMTP { get => Unavailable<string>(); set => Unavailable(); }
    public virtual string WelcomePOP3 { get => Unavailable<string>(); set => Unavailable(); }
    public virtual string WelcomeIMAP { get => Unavailable<string>(); set => Unavailable(); }
    public virtual bool ServiceSMTP { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool ServicePOP3 { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool ServiceIMAP { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual int MaxDeliveryThreads { get => Unavailable<int>(); set => Unavailable(); }
    public virtual IInterfaceAntiVirus AntiVirus => Unavailable<IInterfaceAntiVirus>();
    public virtual IInterfaceRoutes Routes => Unavailable<IInterfaceRoutes>();
    public virtual string HostName { get => Unavailable<string>(); set => Unavailable(); }
    public virtual bool SMTPRelayerRequiresAuthentication { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual string SMTPRelayerUsername { get => Unavailable<string>(); set => Unavailable(); }
    public void SetSMTPRelayerPassword(string newVal) => Unavailable();
    public virtual int SMTPRelayerPort { get => Unavailable<int>(); set => Unavailable(); }
    public virtual string UserInterfaceLanguage { get => Unavailable<string>(); set => Unavailable(); }
    public virtual IInterfaceScripting Scripting => Unavailable<IInterfaceScripting>();
    public virtual int MaxMessageSize { get => Unavailable<int>(); set => Unavailable(); }
    public virtual IInterfaceCache Cache => Unavailable<IInterfaceCache>();
    public virtual int RuleLoopLimit { get => Unavailable<int>(); set => Unavailable(); }
    public virtual IInterfaceBackupSettings Backup => Unavailable<IInterfaceBackupSettings>();
    public virtual string DefaultDomain { get => Unavailable<string>(); set => Unavailable(); }
    public virtual string SMTPDeliveryBindToIP { get => Unavailable<string>(); set => Unavailable(); }
    public virtual int MaxIMAPConnections { get => Unavailable<int>(); set => Unavailable(); }
    public virtual bool IMAPSortEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool IMAPQuotaEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool IMAPIdleEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual int WorkerThreadPriority { get => Unavailable<int>(); set => Unavailable(); }
    public virtual int TCPIPThreads { get => Unavailable<int>(); set => Unavailable(); }
    public virtual bool AllowIncorrectLineEndings { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual int MaxSMTPRecipientsInBatch { get => Unavailable<int>(); set => Unavailable(); }
    public virtual IInterfaceAntiSpam AntiSpam => Unavailable<IInterfaceAntiSpam>();
    public virtual bool DisconnectInvalidClients { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual int MaxNumberOfInvalidCommands { get => Unavailable<int>(); set => Unavailable(); }
    public virtual IInterfaceServerMessages ServerMessages => Unavailable<IInterfaceServerMessages>();
    public virtual IInterfaceTCPIPPorts TCPIPPorts => Unavailable<IInterfaceTCPIPPorts>();
    public virtual bool SMTPRelayerUseSSL { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual IInterfaceSSLCertificates SSLCertificates => Unavailable<IInterfaceSSLCertificates>();
    public virtual bool AddDeliveredToHeader { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual string IMAPPublicFolderName { get => Unavailable<string>(); set => Unavailable(); }
    public virtual bool IMAPACLEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public void SetAdministratorPassword(string newVal) => Unavailable();
    public virtual IInterfaceDirectories Directories => Unavailable<IInterfaceDirectories>();
    public virtual IInterfaceIMAPFolders PublicFolders => Unavailable<IInterfaceIMAPFolders>();
    public virtual string PublicFolderDiskName => Unavailable<string>();
    public virtual IInterfaceGroups Groups => Unavailable<IInterfaceGroups>();
    public virtual IInterfaceIncomingRelays IncomingRelays => Unavailable<IInterfaceIncomingRelays>();
    public virtual bool AutoBanOnLogonFailure { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual int MaxInvalidLogonAttempts { get => Unavailable<int>(); set => Unavailable(); }
    public virtual int MaxInvalidLogonAttemptsWithin { get => Unavailable<int>(); set => Unavailable(); }
    public virtual int AutoBanMinutes { get => Unavailable<int>(); set => Unavailable(); }
    public virtual void ClearLogonFailureList() => Unavailable();
    public virtual string IMAPHierarchyDelimiter { get => Unavailable<string>(); set => Unavailable(); }
    public virtual int MaxAsynchronousThreads { get => Unavailable<int>(); set => Unavailable(); }
    public virtual IInterfaceMessageIndexing MessageIndexing => Unavailable<IInterfaceMessageIndexing>();
    public virtual int MaxNumberOfMXHosts { get => Unavailable<int>(); set => Unavailable(); }
    public virtual ComConnectionSecurity SMTPRelayerConnectionSecurity { get => Unavailable<ComConnectionSecurity>(); set => Unavailable(); }
    public virtual ComConnectionSecurity SMTPConnectionSecurity { get => Unavailable<ComConnectionSecurity>(); set => Unavailable(); }
    public virtual bool VerifyRemoteSslCertificate { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual string SslCipherList { get => Unavailable<string>(); set => Unavailable(); }
    public virtual bool TlsVersion10Enabled { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool TlsVersion11Enabled { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool TlsVersion12Enabled { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual int CrashSimulationMode { get => Unavailable<int>(); set => Unavailable(); }
    public virtual string IMAPMasterUser { get => Unavailable<string>(); set => Unavailable(); }
    public virtual bool IMAPSASLPlainEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool IMAPSASLInitialResponseEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool TlsVersion13Enabled { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool IPv6PreferredEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool TlsOptionPreferServerCiphersEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool TlsOptionPrioritizeChaChaEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool RewriteEnvelopeFromWhenForwarding { get => Unavailable<bool>(); set => Unavailable(); }

    private T Unavailable<T>() => SettingsComAuthorization.Unavailable<T>(this);

    private void Unavailable() => SettingsComAuthorization.Unavailable(this);
}

[ComVisible(false)]
public sealed record SettingsRuntimeConfiguration(
    string UserInterfaceLanguage = "English",
    bool RewriteEnvelopeFromWhenForwarding = false,
    int CrashSimulationMode = 0,
    string LoggingDirectory = "",
    TimeProvider? LoggingTimeProvider = null,
    string ScriptingDirectory = "",
    ILoggingLiveLogRuntime? LoggingLiveLogRuntime = null,
    IScriptSyntaxChecker? ScriptSyntaxChecker = null,
    IScriptRuntimeReloader? ScriptRuntimeReloader = null,
    IClamAvScannerTestRuntime? ClamAvScannerTestRuntime = null,
    IClamWinScannerTestRuntime? ClamWinScannerTestRuntime = null,
    ICustomScannerTestRuntime? CustomScannerTestRuntime = null,
    IDkimVerificationRuntime? DkimVerificationRuntime = null,
    IGreyListingTripletAdministrationStore? GreyListingTripletAdministrationStore = null,
    ISpamAssassinConnectionTestRuntime? SpamAssassinConnectionTestRuntime = null,
    ILogonFailureAdministrationStore? LogonFailureAdministrationStore = null);

[ComVisible(false)]
public static class SettingsAdministrationRuntimeHost
{
    private sealed record RuntimeConfiguration(
        ISettingsAdministrationStore Store,
        SettingsRuntimeConfiguration Settings,
        ISettingsAdministrationMutationStore? MutationStore);

    private static RuntimeConfiguration? _configuration;

    public static void Configure(
        ISettingsAdministrationStore store,
        SettingsRuntimeConfiguration? settings = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(
            ref _configuration,
            new RuntimeConfiguration(
                store,
                settings ?? new SettingsRuntimeConfiguration(),
                store as ISettingsAdministrationMutationStore));
    }

    internal static Settings CreateAuthorizedAdapter(Func<bool>? isServerAdministrator = null)
    {
        var configuration = Volatile.Read(ref _configuration);
        if (configuration is null)
        {
            return Settings.CreateAuthorized(isServerAdministrator);
        }

        var snapshot = configuration.Store
            .GetSettingsAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return Settings.CreateAuthorized(
            snapshot,
            configuration.Settings,
            isServerAdministrator,
            configuration.MutationStore);
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
