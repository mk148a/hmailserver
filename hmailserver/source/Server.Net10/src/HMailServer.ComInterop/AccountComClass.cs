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
    private readonly object _lastLogonUpdate = DateTime.Now;
    private bool _vacationMessageExpires;
    private string _vacationMessageExpiresDate = string.Empty;
    private string _personFirstName = string.Empty;
    private string _personLastName = string.Empty;
    private bool _vacationMessageAbortSpamFlagged;
    private bool _forwardAbortSpamFlagged;
    private int _id;
    private readonly Func<AccountAdministrationSnapshot, string, int>? _save;
    private readonly Action<int>? _delete;
    private AccountAdministrationSnapshot? _currentSaveSnapshot;
    private readonly Func<AccountAdministrationSnapshot, string?, bool>? _update;
    private bool _passwordModified;

    public Account()
    {
    }

    private Account(
        string address,
        ComAdminLevel adminLevel,
        int domainId,
        RuleAdministrationState rulesState,
        Func<AccountAdministrationSnapshot, string, int>? save,
        Action<int>? delete,
        Func<bool>? isServerAdministrator)
    {
        _attached = true;
        _address = address;
        _adminLevel = adminLevel;
        _domainId = domainId;
        _rulesState = rulesState;
        _save = save;
        _delete = delete;
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
        Func<int, AccountAdministrationSnapshot?>? accountSizeReadback,
        Action<int>? delete = null,
        Func<AccountAdministrationSnapshot, string?, bool>? update = null)
    {
        _update = update;
        _attached = true;
        _administrationSnapshot = administrationSnapshot;
        _delete = delete;
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
        get => CurrentSnapshot?.Active ?? Read(_active);
        set => Set(() => _active = value, s => s with { Active = value });
    }

    public string ADDomain { get => CurrentSnapshot?.ActiveDirectoryDomain ?? Read(_activeDirectoryDomain); set => Set(() => _activeDirectoryDomain = value, s => s with { ActiveDirectoryDomain = value ?? string.Empty }); }

    public string Address
    {
        get => CurrentSnapshot?.Address ?? Read(_address);
        set => Set(() => _address = value, s => s with { Address = value ?? string.Empty });
    }

    public int DomainID
    {
        get => CurrentSnapshot?.DomainId ?? Read(_domainId);
        set => Set(() => _domainId = value, s => s with { DomainId = value });
    }

    public int ID
    {
        get
        {
            EnsureAttached();
            return CurrentSnapshot?.Id ?? _id;
        }
    }

    public bool IsAD { get => CurrentSnapshot?.IsActiveDirectoryAccount ?? Read(_isActiveDirectoryAccount); set => Set(() => _isActiveDirectoryAccount = value, s => s with { IsActiveDirectoryAccount = value }); }

    public string Password { get => Read(_password); set => Set(() => { _password = value; _passwordModified = true; }, s => s); }

    public float Size => _administrationSnapshot is not null
        ? GetAccountSize().Size
        : Read(0f);

    public string ADUsername { get => CurrentSnapshot?.ActiveDirectoryUsername ?? Read(_activeDirectoryUsername); set => Set(() => _activeDirectoryUsername = value, s => s with { ActiveDirectoryUsername = value ?? string.Empty }); }

    public IInterfaceMessages Messages
    {
        get
        {
            EnsureAttached();

            return _messagesState is { } messagesState
                ? MessageAdministrationRuntimeHost.CreateAuthorizedAccountAdapter(messagesState, _isAuthenticated)
                : NotImplemented<IInterfaceMessages>();
        }
    }

    public int MaxSize { get => CurrentSnapshot?.MaxSize ?? Read(_maxSize); set => Set(() => _maxSize = value, s => s with { MaxSize = value }); }

    public bool VacationMessageIsOn { get => CurrentSnapshot?.VacationMessageIsOn ?? Read(_vacationMessageIsOn); set => Set(() => _vacationMessageIsOn = value, s => s with { VacationMessageIsOn = value }); }

    public string VacationMessage { get => CurrentSnapshot?.VacationMessage ?? Read(_vacationMessage); set => Set(() => _vacationMessage = value, s => s with { VacationMessage = value ?? string.Empty }); }

    public string VacationSubject { get => CurrentSnapshot?.VacationSubject ?? Read(_vacationSubject); set => Set(() => _vacationSubject = value, s => s with { VacationSubject = value ?? string.Empty }); }

    public IInterfaceFetchAccounts FetchAccounts
    {
        get
        {
            EnsureAttached();

            return _administrationSnapshot is { } account
                ? FetchAccountAdministrationRuntimeHost.CreateAuthorizedAdapter(account.Id, _isAuthenticated)
                : NotImplemented<IInterfaceFetchAccounts>();
        }
    }

    public ComAdminLevel AdminLevel
    {
        get => CurrentSnapshot is { } account
            ? (ComAdminLevel)account.AdminLevel
            : Read(_adminLevel);
        set => Set(() => _adminLevel = value, s => s with { AdminLevel = (int)value });
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
                _isAuthenticated);
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

    public bool ForwardEnabled { get => CurrentSnapshot?.ForwardEnabled ?? Read(_forwardEnabled); set => Set(() => _forwardEnabled = value, s => s with { ForwardEnabled = value }); }

    public string ForwardAddress { get => CurrentSnapshot?.ForwardAddress ?? Read(_forwardAddress); set => Set(() => _forwardAddress = value, s => s with { ForwardAddress = value ?? string.Empty }); }

    public bool ForwardKeepOriginal { get => CurrentSnapshot?.ForwardKeepOriginal ?? Read(_forwardKeepOriginal); set => Set(() => _forwardKeepOriginal = value, s => s with { ForwardKeepOriginal = value }); }

    public bool SignatureEnabled { get => CurrentSnapshot?.SignatureEnabled ?? Read(_signatureEnabled); set => Set(() => _signatureEnabled = value, s => s with { SignatureEnabled = value }); }

    public string SignaturePlainText { get => CurrentSnapshot?.SignaturePlainText ?? Read(_signaturePlainText); set => Set(() => _signaturePlainText = value, s => s with { SignaturePlainText = value ?? string.Empty }); }

    public string SignatureHTML { get => CurrentSnapshot?.SignatureHtml ?? Read(_signatureHtml); set => Set(() => _signatureHtml = value, s => s with { SignatureHtml = value ?? string.Empty }); }

    public object LastLogonTime => CurrentSnapshot?.LastLogonTime ?? Read(_lastLogonUpdate);

    public bool VacationMessageExpires { get => CurrentSnapshot?.VacationMessageExpires ?? Read(_vacationMessageExpires); set => Set(() => _vacationMessageExpires = value, s => s with { VacationMessageExpires = value }); }

    public string VacationMessageExpiresDate { get => CurrentSnapshot?.VacationMessageExpiresDate ?? Read(_vacationMessageExpiresDate); set => Set(() => _vacationMessageExpiresDate = value, s => s with { VacationMessageExpiresDate = value ?? string.Empty }); }

    public string PersonFirstName { get => CurrentSnapshot?.PersonFirstName ?? Read(_personFirstName); set => Set(() => _personFirstName = value, s => s with { PersonFirstName = value ?? string.Empty }); }

    public string PersonLastName { get => CurrentSnapshot?.PersonLastName ?? Read(_personLastName); set => Set(() => _personLastName = value, s => s with { PersonLastName = value ?? string.Empty }); }

    public bool VacationMessageAbortSpamFlagged { get => CurrentSnapshot?.VacationMessageAbortSpamFlagged ?? Read(_vacationMessageAbortSpamFlagged); set => Set(() => _vacationMessageAbortSpamFlagged = value, s => s with { VacationMessageAbortSpamFlagged = value }); }

    public bool ForwardAbortSpamFlagged { get => CurrentSnapshot?.ForwardAbortSpamFlagged ?? Read(_forwardAbortSpamFlagged); set => Set(() => _forwardAbortSpamFlagged = value, s => s with { ForwardAbortSpamFlagged = value }); }

    internal static Account CreateServerAdministrator(Func<bool>? isServerAdministrator = null) =>
        new(
            "Administrator",
            ComAdminLevel.ServerAdministrator,
            0,
            RuleAdministrationRuntimeHost.CreateAuthorizedState(0),
            save: null,
            delete: null,
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
        Func<int, AccountAdministrationSnapshot?>? accountSizeReadback = null,
        Action<int>? delete = null,
        Func<AccountAdministrationSnapshot, string?, bool>? update = null) =>
        new(
            account,
            rulesState,
            messagesState,
            imapFoldersState,
            isAuthenticated,
            accountSizeInvalidator,
            accountSizeReadback,
            delete,
            update);

    internal static Account CreateAuthorizedDraft(
        string address,
        ComAdminLevel adminLevel,
        int domainId,
        Func<AccountAdministrationSnapshot, string, int> save,
        Action<int>? delete = null,
        Func<bool>? isServerAdministrator = null) =>
        new(
            address,
            adminLevel,
            domainId,
            RuleAdministrationRuntimeHost.CreateAuthorizedState(0),
            save,
            delete,
            isServerAdministrator);
    public void Save()
    {
        EnsureAttached();
        EnsureAuthenticated();
        if (_administrationSnapshot is not null)
        {
            if (_update is null)
            {
                NotImplemented();
                return;
            }

            try
            {
                if (!_update(
                    CurrentSnapshot ?? _administrationSnapshot,
                    _passwordModified ? _password : null))
                {
                    throw new InvalidOperationException(
                        "The account update did not affect the selected database row.");
                }
            }
            catch (COMException)
            {
                throw;
            }
            catch (Exception)
            {
                throw new COMException(
                    "It was not possible to save the account to the database.",
                    EFail);
            }

            return;
        }

        if (_save is null)
        {
            NotImplemented();
            return;
        }

        try
        {
            _id = _save(BuildDraftSnapshot(), _password);
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the account to the database.",
                EFail);
        }
    }

    private AccountAdministrationSnapshot BuildDraftSnapshot() =>
        new(
            Id: 0,
            DomainId: _domainId,
            Address: _address,
            Active: _active,
            AdminLevel: (int)_adminLevel,
            IsActiveDirectoryAccount: _isActiveDirectoryAccount,
            ActiveDirectoryDomain: _activeDirectoryDomain,
            ActiveDirectoryUsername: _activeDirectoryUsername,
            MaxSize: _maxSize,
            LastLogonTime: (DateTime)_lastLogonUpdate,
            PersonFirstName: _personFirstName,
            PersonLastName: _personLastName,
            VacationMessageIsOn: _vacationMessageIsOn,
            VacationMessage: _vacationMessage,
            VacationSubject: _vacationSubject,
            VacationMessageExpires: _vacationMessageExpires,
            VacationMessageExpiresDate: _vacationMessageExpiresDate,
            VacationMessageAbortSpamFlagged: _vacationMessageAbortSpamFlagged,
            ForwardEnabled: _forwardEnabled,
            ForwardAddress: _forwardAddress,
            ForwardKeepOriginal: _forwardKeepOriginal,
            ForwardAbortSpamFlagged: _forwardAbortSpamFlagged,
            SignatureEnabled: _signatureEnabled,
            SignaturePlainText: _signaturePlainText,
            SignatureHtml: _signatureHtml);

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

        public void Delete()
    {
        EnsureAttached();
        EnsureAuthenticated();
        if (_delete is null)
        {
            NotImplemented();
            return;
        }

        _delete(ID);
    }

    private AccountAdministrationSnapshot? CurrentSnapshot
    {
        get
        {
            if (_administrationSnapshot is not null)
            {
                EnsureAuthenticated();
            }

            return _currentSaveSnapshot ?? _administrationSnapshot;
        }
    }

    private void Set(
        Action assign,
        Func<AccountAdministrationSnapshot, AccountAdministrationSnapshot> morph)
    {
        EnsureAttached();
        if (_administrationSnapshot is not null)
        {
            EnsureAuthenticated();
        }

        assign();
        if (_administrationSnapshot is not null)
        {
            _currentSaveSnapshot = morph(CurrentSnapshot ?? _administrationSnapshot);
        }
    }

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
        EnsureAuthenticated();
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
