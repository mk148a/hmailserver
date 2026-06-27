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
    private bool _isServerAdministrator;

    public Application()
    {
    }

    internal Application(IServerAdministratorAuthenticationProvider authenticationProvider)
    {
        ArgumentNullException.ThrowIfNull(authenticationProvider);
        _authenticationProvider = authenticationProvider;
    }

    [ComVisible(false)]
    public static Application CreateForRuntime(IServerAdministratorAuthenticationProvider authenticationProvider) =>
        new(authenticationProvider);

    public IInterfaceSettings Settings
    {
        get
        {
            EnsureServerAdministrator();
            return HMailServer.ComInterop.Settings.CreateAuthorized();
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

    public ComServerState ServerState => NotImplemented<ComServerState>();

    public IInterfaceDatabase Database => NotImplemented<IInterfaceDatabase>();

    public IInterfaceUtilities Utilities => NotImplemented<IInterfaceUtilities>();

    public IInterfaceStatus Status => NotImplemented<IInterfaceStatus>();

    public string Version => NotImplemented<string>();

    public string InitializationFile => NotImplemented<string>();

    public IInterfaceRules Rules
    {
        get
        {
            EnsureServerAdministrator();
            return RuleAdministrationRuntimeHost.CreateAuthorizedAdapter(accountId: 0);
        }
    }

    public IInterfaceBackupManager BackupManager => NotImplemented<IInterfaceBackupManager>();

    public IInterfaceGlobalObjects GlobalObjects => NotImplemented<IInterfaceGlobalObjects>();

    public IInterfaceLinks Links => NotImplemented<IInterfaceLinks>();

    public IInterfaceDiagnostics Diagnostics => NotImplemented<IInterfaceDiagnostics>();

    public string VersionArchitecture => Environment.Is64BitProcess ? "64-bit" : "32-bit";

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
