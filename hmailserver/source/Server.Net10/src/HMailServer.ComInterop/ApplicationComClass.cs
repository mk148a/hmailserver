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
    private bool _isServerAdministrator;

    public Application()
    {
    }

    internal Application(
        IServerAdministratorAuthenticationProvider authenticationProvider,
        IBackupArchiveMetadataReader? backupArchiveMetadataReader = null,
        ILegacyBlowfishCipher? legacyBlowfishCipher = null,
        ILocalHostRuntime? localHostRuntime = null)
    {
        ArgumentNullException.ThrowIfNull(authenticationProvider);
        _authenticationProvider = authenticationProvider;
        _backupArchiveMetadataReader = backupArchiveMetadataReader;
        _legacyBlowfishCipher = legacyBlowfishCipher;
        _localHostRuntime = localHostRuntime;
    }

    [ComVisible(false)]
    public static Application CreateForRuntime(
        IServerAdministratorAuthenticationProvider authenticationProvider,
        ILegacyBlowfishCipher? legacyBlowfishCipher = null,
        ILocalHostRuntime? localHostRuntime = null) =>
        new(
            authenticationProvider,
            legacyBlowfishCipher: legacyBlowfishCipher,
            localHostRuntime: localHostRuntime);

    public IInterfaceSettings Settings
    {
        get
        {
            EnsureServerAdministrator();
            return SettingsAdministrationRuntimeHost.CreateAuthorizedAdapter();
        }
    }

    public IInterfaceDomains Domains
    {
        get
        {
            EnsureServerAdministrator();
            return DomainAdministrationRuntimeHost.CreateAuthorizedAdapter();
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
            _localHostRuntime);

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
            return RuleAdministrationRuntimeHost.CreateAuthorizedAdapter(accountId: 0);
        }
    }

    public IInterfaceBackupManager BackupManager
    {
        get
        {
            EnsureServerAdministrator();
            return HMailServer.ComInterop.BackupManager.CreateAuthorized(_backupArchiveMetadataReader);
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

        _isServerAdministrator = provider.Authenticate(username, password);
        return _isServerAdministrator ? Account.CreateServerAdministrator() : null;
    }

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
