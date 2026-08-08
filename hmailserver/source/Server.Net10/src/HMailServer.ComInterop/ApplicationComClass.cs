using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("D6567EF8-0A6C-48E7-9288-A2463123C2F3")]
[ProgId("hMailServer.Application.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceApplication))]
public sealed class Application : IInterfaceApplication
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly IServerAdministratorAuthenticationProvider? _authenticationProvider;
    private readonly IBackupArchiveMetadataReader? _backupArchiveMetadataReader;
    private readonly ILegacyBlowfishCipher? _legacyBlowfishCipher;
    private readonly ILocalHostRuntime? _localHostRuntime;
    private readonly IMailServerResolver? _mailServerResolver;
    private readonly IMessageIdResolver? _messageIdResolver;
    private readonly IImapFolderUidMaintenanceStore? _imapFolderUidMaintenanceStore;
    private readonly IServiceDependencyRuntime? _serviceDependencyRuntime;
    private readonly IEmailAllAccountsRuntime? _emailAllAccountsRuntime;
    private readonly IImportMessageFromFileRuntime? _importMessageFromFileRuntime;
    private bool _isServerAdministrator;
    private long _authenticationGeneration;

    public Application()
    {
    }

    internal Application(
        IServerAdministratorAuthenticationProvider authenticationProvider,
        IBackupArchiveMetadataReader? backupArchiveMetadataReader = null,
        ILegacyBlowfishCipher? legacyBlowfishCipher = null,
        ILocalHostRuntime? localHostRuntime = null,
        IMailServerResolver? mailServerResolver = null,
        IMessageIdResolver? messageIdResolver = null,
        IImapFolderUidMaintenanceStore? imapFolderUidMaintenanceStore = null,
        IServiceDependencyRuntime? serviceDependencyRuntime = null,
        IEmailAllAccountsRuntime? emailAllAccountsRuntime = null,
        IImportMessageFromFileRuntime? importMessageFromFileRuntime = null)
    {
        ArgumentNullException.ThrowIfNull(authenticationProvider);
        _authenticationProvider = authenticationProvider;
        _backupArchiveMetadataReader = backupArchiveMetadataReader;
        _legacyBlowfishCipher = legacyBlowfishCipher;
        _localHostRuntime = localHostRuntime;
        _mailServerResolver = mailServerResolver;
        _messageIdResolver = messageIdResolver;
        _imapFolderUidMaintenanceStore = imapFolderUidMaintenanceStore;
        _serviceDependencyRuntime = serviceDependencyRuntime;
        _emailAllAccountsRuntime = emailAllAccountsRuntime;
        _importMessageFromFileRuntime = importMessageFromFileRuntime;
    }

    [ComVisible(false)]
    public static Application CreateForRuntime(
        IServerAdministratorAuthenticationProvider authenticationProvider,
        ILegacyBlowfishCipher? legacyBlowfishCipher = null,
        ILocalHostRuntime? localHostRuntime = null,
        IMailServerResolver? mailServerResolver = null,
        IMessageIdResolver? messageIdResolver = null,
        IImapFolderUidMaintenanceStore? imapFolderUidMaintenanceStore = null,
        IServiceDependencyRuntime? serviceDependencyRuntime = null,
        IEmailAllAccountsRuntime? emailAllAccountsRuntime = null,
        IImportMessageFromFileRuntime? importMessageFromFileRuntime = null) =>
        new(
            authenticationProvider,
            legacyBlowfishCipher: legacyBlowfishCipher,
            localHostRuntime: localHostRuntime,
            mailServerResolver: mailServerResolver,
            messageIdResolver: messageIdResolver,
            imapFolderUidMaintenanceStore: imapFolderUidMaintenanceStore,
            serviceDependencyRuntime: serviceDependencyRuntime,
            emailAllAccountsRuntime: emailAllAccountsRuntime,
            importMessageFromFileRuntime: importMessageFromFileRuntime);

    public IInterfaceSettings Settings
    {
        get
        {
            EnsureServerAdministrator();
            return SettingsAdministrationRuntimeHost.CreateAuthorizedAdapter(() => _isServerAdministrator);
        }
    }

    public IInterfaceDomains Domains
    {
        get
        {
            EnsureServerAdministrator();
            return DomainAdministrationRuntimeHost.CreateAuthorizedAdapter(() => _isServerAdministrator);
        }
    }

    public ComServerState ServerState
    {
        get
        {
            EnsureServerAdministrator();
            return (ComServerState)ApplicationRuntimeHost.Snapshot.ServerState;
        }
    }

    public IInterfaceDatabase Database =>
        DatabaseAdministrationRuntimeHost.CreateApplicationAdapter(() => _isServerAdministrator);

    public IInterfaceUtilities Utilities =>
        HMailServer.ComInterop.Utilities.CreateForApplication(
            () => _isServerAdministrator,
            _legacyBlowfishCipher,
            _localHostRuntime,
            _mailServerResolver,
            _messageIdResolver,
            _imapFolderUidMaintenanceStore,
            _serviceDependencyRuntime,
            _emailAllAccountsRuntime,
            _importMessageFromFileRuntime);

    public IInterfaceStatus Status
    {
        get
        {
            EnsureServerAdministrator();
            return StatusAdministrationRuntimeHost.CreateAuthorizedAdapter();
        }
    }

    public string Version => ApplicationRuntimeHost.Snapshot.Version;

    public string InitializationFile
    {
        get
        {
            EnsureServerAdministrator();
            return ApplicationRuntimeHost.Snapshot.InitializationFile;
        }
    }

    public IInterfaceRules Rules
    {
        get
        {
            EnsureServerAdministrator();
            return RuleAdministrationRuntimeHost.CreateAuthorizedAdapter(
                accountId: 0,
                isServerAdministrator: () => _isServerAdministrator,
                isAuthenticated: () => _isServerAdministrator);
        }
    }

    public IInterfaceBackupManager BackupManager
    {
        get
        {
            EnsureServerAdministrator();
            var generation = Volatile.Read(ref _authenticationGeneration);
            return HMailServer.ComInterop.BackupManager.CreateAuthorized(
                _backupArchiveMetadataReader,
                authorizationGuard: () => IsCurrentAdministrator(generation));
        }
    }

    public IInterfaceGlobalObjects GlobalObjects
    {
        get
        {
            EnsureServerAdministrator();
            return HMailServer.ComInterop.GlobalObjects.CreateAuthorized();
        }
    }

    public IInterfaceLinks Links
    {
        get
        {
            EnsureServerAdministrator();
            return LinksAdministrationRuntimeHost.CreateAuthorizedAdapter();
        }
    }

    public IInterfaceDiagnostics Diagnostics
    {
        get
        {
            EnsureServerAdministrator();
            return DiagnosticsRuntimeHost.CreateAuthorizedAdapter();
        }
    }

    public string VersionArchitecture => ApplicationRuntimeHost.Snapshot.VersionArchitecture;

    public void Start() => NotImplemented();

    public void Stop() => NotImplemented();

    public void SubmitEMail() => NotImplemented();

    public void Connect() => NotImplemented();

    public void Reinitialize() => NotImplemented();

    public IInterfaceAccount? Authenticate(string username, string password)
    {
        var provider = _authenticationProvider
            ?? throw new COMException(
                "The hMailServer COM authentication runtime has not been initialized.",
                CoENotInitialized);

        var generation = Interlocked.Increment(ref _authenticationGeneration);
        Volatile.Write(ref _isServerAdministrator, false);
        var isServerAdministrator = provider.Authenticate(username, password);
        if (Volatile.Read(ref _authenticationGeneration) != generation)
        {
            return null;
        }

        Volatile.Write(ref _isServerAdministrator, isServerAdministrator);
        return isServerAdministrator
            ? Account.CreateServerAdministrator(() => _isServerAdministrator)
            : null;
    }

    private bool IsCurrentAdministrator(long generation) =>
        Volatile.Read(ref _isServerAdministrator)
        && Volatile.Read(ref _authenticationGeneration) == generation;

    private void EnsureServerAdministrator()
    {
        if (!_isServerAdministrator)
        {
            throw new COMException(
                "You do not have access to this property / method. Ensure that hMailServer.Application.Authenticate() is called with proper login credentials.",
                EAccessDenied);
        }
    }

    private static void NotImplemented() =>
        throw new COMException("This legacy COM member has not been implemented by the .NET 10 rewrite.", ENotImplemented);

    private static T NotImplemented<T>() =>
        throw new COMException("This legacy COM member has not been implemented by the .NET 10 rewrite.", ENotImplemented);
}

[ComVisible(false)]
public static class ApplicationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static IApplicationRuntimeStore? _store;

    public static void Configure(IApplicationRuntimeStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static ApplicationRuntimeSnapshot Snapshot
    {
        get
        {
            var store = Volatile.Read(ref _store)
                ?? throw new COMException(
                    "The hMailServer application runtime has not been initialized.",
                    CoENotInitialized);

            return store.GetSnapshot();
        }
    }
}
