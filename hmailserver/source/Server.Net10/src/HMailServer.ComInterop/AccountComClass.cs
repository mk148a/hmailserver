using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("369BE902-9F27-4722-A29F-3059E4D7021D")]
[ProgId("hMailServer.Account.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceAccount))]
public sealed class Account : IInterfaceAccount
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly bool _attached;
    private readonly AccountAdministrationSnapshot? _administrationSnapshot;
    private readonly AccountSizeInvalidator? _accountSizeInvalidator;
    private readonly Func<int, AccountAdministrationSnapshot?>? _accountSizeReadback;
    private readonly object _accountSizeGate = new();
    private AccountSizeState? _accountSizeState;
    private readonly ImapFolderAdministrationState? _imapFoldersState;
    private readonly AccountMessageAdministrationState? _messagesState;
    private readonly RuleAdministrationState? _rulesState;
    private readonly Func<bool>? _isServerAdministrator;
    private readonly Func<bool>? _isAuthenticated;
    private bool _active;
    private string _activeDirectoryDomain = string.Empty;
    private string _address = string.Empty;
    private int _domainId;
    private bool _isActiveDirectoryAccount;
    private string _password = string.Empty;
    private string _activeDirectoryUsername = string.Empty;
    private int _maxSize;
    private bool _vacationMessageIsOn;
    private string _vacationMessage = string.Empty;
    private string _vacationSubject = string.Empty;
    private ComAdminLevel _adminLevel;
    private bool _forwardEnabled;
    private string _forwardAddress = string.Empty;
    private bool _forwardKeepOriginal;
    private bool _signatureEnabled;
    private string _signaturePlainText = string.Empty;
    private string _signatureHtml = string.Empty;
    private readonly object _lastLogonTime = DateTime.Now;
    private bool _vacationMessageExpires;
    private string _vacationMessageExpiresDate = string.Empty;
    private string _personFirstName = string.Empty;
    private string _personLastName = string.Empty;
    private bool _vacationMessageAbortSpamFlagged;
    private bool _forwardAbortSpamFlagged;

    public Account()
    {
    }

    private Account(
        string address,
        ComAdminLevel adminLevel,
        RuleAdministrationState rulesState,
        Func<bool>? isServerAdministrator)
    {
        _attached = true;
        _address = address;
        _adminLevel = adminLevel;
        _rulesState = rulesState;
        _isServerAdministrator = isServerAdministrator;
        _isAuthenticated = isServerAdministrator;
    }

    private Account(
        AccountAdministrationSnapshot administrationSnapshot,
        RuleAdministrationState rulesState,
        AccountMessageAdministrationState messagesState,
        ImapFolderAdministrationState imapFoldersState,
        Func<bool>? isAuthenticated,
        AccountSizeInvalidator? accountSizeInvalidator,
        Func<int, AccountAdministrationSnapshot?>? accountSizeReadback)
    {
        _attached = true;
        _administrationSnapshot = administrationSnapshot;
        _accountSizeInvalidator = accountSizeInvalidator;
        _accountSizeReadback = accountSizeReadback;
        _accountSizeState = new AccountSizeState(administrationSnapshot.Size, administrationSnapshot.QuotaUsed, 0);
        _imapFoldersState = imapFoldersState;
        _messagesState = messagesState;
        _rulesState = rulesState;
        _isAuthenticated = isAuthenticated;
    }

    public bool Active
    {
        get => _administrationSnapshot?.Active ?? Read(_active);
        set => Write(() => _active = value);
    }

    public string ADDomain { get => _administrationSnapshot?.ActiveDirectoryDomain ?? Read(_activeDirectoryDomain); set => Write(() => _activeDirectoryDomain = value); }

    public string Address
    {
        get => _administrationSnapshot?.Address ?? Read(_address);
        set => Write(() => _address = value);
    }

    public int DomainID
    {
        get => _administrationSnapshot?.DomainId ?? Read(_domainId);
        set => Write(() => _domainId = value);
    }

    public int ID => _administrationSnapshot?.Id ?? Read(0);

    public bool IsAD { get => _administrationSnapshot?.IsActiveDirectoryAccount ?? Read(_isActiveDirectoryAccount); set => Write(() => _isActiveDirectoryAccount = value); }

    public string Password { get => Read(_password); set => Write(() => _password = value); }

    public float Size => _administrationSnapshot is not null
        ? GetAccountSize().Size
        : Read(0f);

    public string ADUsername { get => _administrationSnapshot?.ActiveDirectoryUsername ?? Read(_activeDirectoryUsername); set => Write(() => _activeDirectoryUsername = value); }

    public IInterfaceMessages Messages
    {
        get
        {
            EnsureAttached();

            return _messagesState is { } messagesState
                ? MessageAdministrationRuntimeHost.CreateAuthorizedAccountAdapter(messagesState)
                : NotImplemented<IInterfaceMessages>();
        }
    }

    public int MaxSize { get => _administrationSnapshot?.MaxSize ?? Read(_maxSize); set => Write(() => _maxSize = value); }

    public bool VacationMessageIsOn { get => _administrationSnapshot?.VacationMessageIsOn ?? Read(_vacationMessageIsOn); set => Write(() => _vacationMessageIsOn = value); }

    public string VacationMessage { get => _administrationSnapshot?.VacationMessage ?? Read(_vacationMessage); set => Write(() => _vacationMessage = value); }

    public string VacationSubject { get => _administrationSnapshot?.VacationSubject ?? Read(_vacationSubject); set => Write(() => _vacationSubject = value); }

    public IInterfaceFetchAccounts FetchAccounts
    {
        get
        {
            EnsureAttached();

            return _administrationSnapshot is { } account
                ? FetchAccountAdministrationRuntimeHost.CreateAuthorizedAdapter(account.Id)
                : NotImplemented<IInterfaceFetchAccounts>();
        }
    }

    public ComAdminLevel AdminLevel
    {
        get => _administrationSnapshot is { } account
            ? (ComAdminLevel)account.AdminLevel
            : Read(_adminLevel);
        set => Write(() => _adminLevel = value);
    }

    public IInterfaceRules Rules
    {
        get
        {
            EnsureAttached();

            if (_rulesState is null)
            {
                return NotImplemented<IInterfaceRules>();
            }

            _ = _rulesState.GetGeneration();
            return HMailServer.ComInterop.Rules.CreateAuthorized(
                _rulesState,
                _isServerAdministrator,
                _isServerAdministrator);
        }
    }

    public IInterfaceIMAPFolders IMAPFolders
    {
        get
        {
            EnsureAttached();
            EnsureAuthenticated();

            return _administrationSnapshot is { } account && _imapFoldersState is { } foldersState
                ? HMailServer.ComInterop.IMAPFolders.CreateAuthorized(
                    foldersState,
                    account.Id,
                    -1,
                    _isAuthenticated)
                : NotImplemented<IInterfaceIMAPFolders>();
        }
    }

    public int QuotaUsed => _administrationSnapshot is not null
        ? GetAccountSize().QuotaUsed
        : Read(0);

    public bool ForwardEnabled { get => _administrationSnapshot?.ForwardEnabled ?? Read(_forwardEnabled); set => Write(() => _forwardEnabled = value); }

    public string ForwardAddress { get => _administrationSnapshot?.ForwardAddress ?? Read(_forwardAddress); set => Write(() => _forwardAddress = value); }

    public bool ForwardKeepOriginal { get => _administrationSnapshot?.ForwardKeepOriginal ?? Read(_forwardKeepOriginal); set => Write(() => _forwardKeepOriginal = value); }

    public bool SignatureEnabled { get => _administrationSnapshot?.SignatureEnabled ?? Read(_signatureEnabled); set => Write(() => _signatureEnabled = value); }

    public string SignaturePlainText { get => _administrationSnapshot?.SignaturePlainText ?? Read(_signaturePlainText); set => Write(() => _signaturePlainText = value); }

    public string SignatureHTML { get => _administrationSnapshot?.SignatureHtml ?? Read(_signatureHtml); set => Write(() => _signatureHtml = value); }

    public object LastLogonTime => _administrationSnapshot?.LastLogonTime ?? Read(_lastLogonTime);

    public bool VacationMessageExpires { get => _administrationSnapshot?.VacationMessageExpires ?? Read(_vacationMessageExpires); set => Write(() => _vacationMessageExpires = value); }

    public string VacationMessageExpiresDate { get => _administrationSnapshot?.VacationMessageExpiresDate ?? Read(_vacationMessageExpiresDate); set => Write(() => _vacationMessageExpiresDate = value); }

    public string PersonFirstName { get => _administrationSnapshot?.PersonFirstName ?? Read(_personFirstName); set => Write(() => _personFirstName = value); }

    public string PersonLastName { get => _administrationSnapshot?.PersonLastName ?? Read(_personLastName); set => Write(() => _personLastName = value); }

    public bool VacationMessageAbortSpamFlagged { get => _administrationSnapshot?.VacationMessageAbortSpamFlagged ?? Read(_vacationMessageAbortSpamFlagged); set => Write(() => _vacationMessageAbortSpamFlagged = value); }

    public bool ForwardAbortSpamFlagged { get => _administrationSnapshot?.ForwardAbortSpamFlagged ?? Read(_forwardAbortSpamFlagged); set => Write(() => _forwardAbortSpamFlagged = value); }

    internal static Account CreateServerAdministrator(Func<bool>? isServerAdministrator = null) =>
        new(
            "Administrator",
            ComAdminLevel.ServerAdministrator,
            RuleAdministrationRuntimeHost.CreateAuthorizedState(0),
            isServerAdministrator);

    internal static Account CreateAuthorized(
        AccountAdministrationSnapshot account,
        Func<bool>? isAuthenticated = null) =>
        new(
            account,
            RuleAdministrationRuntimeHost.CreateAuthorizedState(account.Id),
            MessageAdministrationRuntimeHost.CreateAuthorizedAccountState(account.Id),
            ImapFolderAdministrationRuntimeHost.CreateAuthorizedState(account.Id),
            isAuthenticated,
            null,
            null);

    internal static Account CreateAuthorized(
        AccountAdministrationSnapshot account,
        RuleAdministrationState rulesState) =>
        new(
            account,
            rulesState,
            MessageAdministrationRuntimeHost.CreateAuthorizedAccountState(account.Id),
            ImapFolderAdministrationRuntimeHost.CreateAuthorizedState(account.Id),
            null,
            null,
            null);

    internal static Account CreateAuthorized(
        AccountAdministrationSnapshot account,
        RuleAdministrationState rulesState,
        AccountMessageAdministrationState messagesState) =>
        new(
            account,
            rulesState,
            messagesState,
            ImapFolderAdministrationRuntimeHost.CreateAuthorizedState(account.Id),
            null,
            null,
            null);

    internal static Account CreateAuthorized(
        AccountAdministrationSnapshot account,
        RuleAdministrationState rulesState,
        AccountMessageAdministrationState messagesState,
        ImapFolderAdministrationState imapFoldersState,
        Func<bool>? isAuthenticated = null,
        AccountSizeInvalidator? accountSizeInvalidator = null,
        Func<int, AccountAdministrationSnapshot?>? accountSizeReadback = null) =>
        new(
            account,
            rulesState,
            messagesState,
            imapFoldersState,
            isAuthenticated,
            accountSizeInvalidator,
            accountSizeReadback);

    public void Save() => NotImplemented();

    public void DeleteMessages() => NotImplemented();

    public bool ValidatePassword(string password)
    {
        EnsureAttached();
        if (_administrationSnapshot is not null)
        {
            throw new COMException("This Account member is not implemented by the .NET 10 rewrite.", ENotImplemented);
        }

        return false;
    }

    public void UnlockMailbox() => NotImplemented();

    public void Delete() => NotImplemented();

    private T Read<T>(T value)
    {
        EnsureAttached();
        if (_administrationSnapshot is not null)
        {
            throw new COMException("This Account member is not implemented by the .NET 10 rewrite.", ENotImplemented);
        }

        return value;
    }

    private AccountSizeState GetAccountSize()
    {
        lock (_accountSizeGate)
        {
            var account = _administrationSnapshot!;
            var state = _accountSizeState!;
            if (_accountSizeInvalidator is null || _accountSizeReadback is null)
            {
                return state;
            }

            var version = _accountSizeInvalidator.GetVersion(account.Id);
            if (version <= state.Version)
            {
                return state;
            }

            try
            {
                var refreshed = _accountSizeReadback(account.Id);
                var updated = refreshed is null
                    ? state with { Version = version }
                    : new AccountSizeState(refreshed.Size, refreshed.QuotaUsed, version);
                _accountSizeState = updated;
                return updated;
            }
            catch (COMException)
            {
                throw;
            }
            catch (Exception)
            {
                throw new COMException(
                    "It was not possible to retrieve the account size from the database.",
                    EFail);
            }
        }
    }

    private void Write(Action assign)
    {
        EnsureAttached();
        if (_administrationSnapshot is not null)
        {
            throw new COMException("This Account member is not implemented by the .NET 10 rewrite.", ENotImplemented);
        }

        assign();
    }

    private void EnsureAttached()
    {
        if (!_attached)
        {
            throw new COMException(
                "You do not have access to this property / method. Ensure that hMailServer.Application.Authenticate() is called with proper login credentials.",
                EAccessDenied);
        }
    }

    private void EnsureAuthenticated()
    {
        if (_isAuthenticated is not null && !_isAuthenticated())
        {
            throw new COMException(
                "Account access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private void NotImplemented()
    {
        EnsureAttached();
        throw new COMException("This legacy COM member has not been implemented by the .NET 10 rewrite.", ENotImplemented);
    }

    private T NotImplemented<T>()
    {
        EnsureAttached();
        throw new COMException("This legacy COM member has not been implemented by the .NET 10 rewrite.", ENotImplemented);
    }

    private sealed record AccountSizeState(float Size, int QuotaUsed, long Version);
}
