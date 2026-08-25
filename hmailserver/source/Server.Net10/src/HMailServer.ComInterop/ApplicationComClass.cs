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
    private const int EFail = unchecked((int)0x80004005);
    private const int ELegacyComError = unchecked((int)0x800403E9);
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
    private readonly Func<CancellationToken, ValueTask>? _reinitializeAsync;
    private readonly Func<CancellationToken, ValueTask>? _startAsync;
    private readonly Func<CancellationToken, ValueTask>? _stopAsync;
    private readonly ApplicationAuthorizationAuthority _authorizationAuthority = new();

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
        IImportMessageFromFileRuntime? importMessageFromFileRuntime = null,
        Func<CancellationToken, ValueTask>? reinitializeAsync = null,
        Func<CancellationToken, ValueTask>? startAsync = null,
        Func<CancellationToken, ValueTask>? stopAsync = null)
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
        _reinitializeAsync = reinitializeAsync;
        _startAsync = startAsync;
        _stopAsync = stopAsync;
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
        IImportMessageFromFileRuntime? importMessageFromFileRuntime = null,
        Func<CancellationToken, ValueTask>? reinitializeAsync = null,
        Func<CancellationToken, ValueTask>? startAsync = null,
        Func<CancellationToken, ValueTask>? stopAsync = null) =>
        new(
            authenticationProvider,
            legacyBlowfishCipher: legacyBlowfishCipher,
            localHostRuntime: localHostRuntime,
            mailServerResolver: mailServerResolver,
            messageIdResolver: messageIdResolver,
            imapFolderUidMaintenanceStore: imapFolderUidMaintenanceStore,
            serviceDependencyRuntime: serviceDependencyRuntime,
            emailAllAccountsRuntime: emailAllAccountsRuntime,
            importMessageFromFileRuntime: importMessageFromFileRuntime,
            reinitializeAsync: reinitializeAsync,
            startAsync: startAsync,
            stopAsync: stopAsync);

    public IInterfaceSettings Settings
    {
        get
        {
            EnsureServerAdministrator();
            var generation = _authorizationAuthority.CurrentGeneration;
            return SettingsAdministrationRuntimeHost.CreateAuthorizedAdapter(
                () => _authorizationAuthority.IsCurrentAdministrator(generation),
                cancellationToken => _authorizationAuthority.AcquireLeaseAsync(generation, cancellationToken));
        }
    }

    public IInterfaceDomains Domains
    {
        get
        {
            EnsureServerAdministrator();
            var generation = _authorizationAuthority.CurrentGeneration;
            return DomainAdministrationRuntimeHost.CreateAuthorizedAdapter(
                () => _authorizationAuthority.IsCurrentAdministrator(generation),
                cancellationToken => _authorizationAuthority.AcquireLeaseAsync(generation, cancellationToken));
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
        DatabaseAdministrationRuntimeHost.CreateApplicationAdapter(() => IsServerAdministrator);

    public IInterfaceUtilities Utilities =>
        HMailServer.ComInterop.Utilities.CreateForApplication(
            () => IsServerAdministrator,
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
                isServerAdministrator: () => IsServerAdministrator,
                isAuthenticated: () => IsServerAdministrator);
        }
    }

    public IInterfaceBackupManager BackupManager
    {
        get
        {
            EnsureServerAdministrator();
            var generation = _authorizationAuthority.CurrentGeneration;
            return HMailServer.ComInterop.BackupManager.CreateAuthorized(
                _backupArchiveMetadataReader,
                authorizationGuard: () => _authorizationAuthority.IsCurrentAdministrator(generation),
                authorizationLeaseFactory: cancellationToken =>
                    _authorizationAuthority.AcquireLeaseAsync(generation, cancellationToken));
        }
    }

    public IInterfaceGlobalObjects GlobalObjects
    {
        get
        {
            EnsureServerAdministrator();
            return HMailServer.ComInterop.GlobalObjects.CreateAuthorized(() => IsServerAdministrator);
        }
    }

    public IInterfaceLinks Links
    {
        get
        {
            EnsureServerAdministrator();
            var generation = _authorizationAuthority.CurrentGeneration;
            return LinksAdministrationRuntimeHost.CreateAuthorizedAdapter(
                () => _authorizationAuthority.IsCurrentAdministrator(generation),
                cancellationToken => _authorizationAuthority.AcquireLeaseAsync(generation, cancellationToken));
        }
    }

    public IInterfaceDiagnostics Diagnostics
    {
        get
        {
            EnsureServerAdministrator();
            return DiagnosticsRuntimeHost.CreateAuthorizedAdapter(() => IsServerAdministrator);
        }
    }

    public string VersionArchitecture => ApplicationRuntimeHost.Snapshot.VersionArchitecture;

    public void Start()
    {
        EnsureServerAdministrator();
        InvokeLifecycle(_startAsync, "start");
    }

    public void Stop()
    {
        EnsureServerAdministrator();
        InvokeLifecycle(_stopAsync, "stop");
    }

    public void SubmitEMail()
    {
        bool signalConfigured;
        try
        {
            signalConfigured = DeliveryQueueAdministrationRuntimeHost.TrySignal();
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to submit pending email.",
                EFail);
        }

        if (!signalConfigured)
        {
            NotImplemented();
        }
    }

    public void Connect()
    {
        var lastErrorMessage = ApplicationRuntimeHost.Snapshot.LastErrorMessage;
        if (!string.IsNullOrEmpty(lastErrorMessage))
        {
            throw new COMException(lastErrorMessage, ELegacyComError);
        }
    }

    public void Reinitialize()
    {
        EnsureServerAdministrator();
        var generation = _authorizationAuthority.CurrentGeneration;
        using var authorizationLease = _authorizationAuthority
            .AcquireLeaseAsync(generation, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult()
            ?? throw new COMException(
                "You do not have access to this property / method. Ensure that hMailServer.Application.Authenticate() is called with proper login credentials.",
                EAccessDenied);

        if (_reinitializeAsync is null)
        {
            NotImplemented();
            return;
        }

        try
        {
            _reinitializeAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to reinitialize the hMailServer service.",
                unchecked((int)0x80004005));
        }
    }

    public IInterfaceAccount? Authenticate(string username, string password)
    {
        var provider = _authenticationProvider
            ?? throw new COMException(
                "The hMailServer COM authentication runtime has not been initialized.",
                CoENotInitialized);

        var attempt = _authorizationAuthority.BeginAuthentication();
        var isServerAdministrator = provider.Authenticate(username, password);
        if (!_authorizationAuthority.CompleteAuthentication(attempt, isServerAdministrator))
        {
            return null;
        }

        return isServerAdministrator
            ? Account.CreateServerAdministrator(
                () => IsServerAdministrator,
                AccountAdministrationRuntimeHost.UnlockMailboxCallback)
            : null;
    }

    private bool IsServerAdministrator => _authorizationAuthority.IsServerAdministrator;

    private void EnsureServerAdministrator()
    {
        if (!IsServerAdministrator)
        {
            throw new COMException(
                "You do not have access to this property / method. Ensure that hMailServer.Application.Authenticate() is called with proper login credentials.",
                EAccessDenied);
        }
    }

    private static void NotImplemented() =>
        throw new COMException("This legacy COM member has not been implemented by the .NET 10 rewrite.", ENotImplemented);

    private void InvokeLifecycle(
        Func<CancellationToken, ValueTask>? lifecycle,
        string operation)
    {
        var generation = _authorizationAuthority.CurrentGeneration;
        using var authorizationLease = _authorizationAuthority
            .AcquireLeaseAsync(generation, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult()
            ?? throw new COMException(
                "You do not have access to this property / method. Ensure that hMailServer.Application.Authenticate() is called with proper login credentials.",
                EAccessDenied);

        if (lifecycle is null)
        {
            NotImplemented();
            return;
        }

        try
        {
            lifecycle(CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                $"It was not possible to {operation} the hMailServer service.",
                EFail);
        }
    }

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
