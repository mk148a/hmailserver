using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("369BE902-9F27-4722-A29F-3059E4D7021D")]
[ProgId("hMailServer.Account.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceAccount))]
public sealed class Account : IInterfaceAccount
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly bool _attached;
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

    private Account(string address, ComAdminLevel adminLevel)
    {
        _attached = true;
        _address = address;
        _adminLevel = adminLevel;
    }

    public bool Active { get => Read(_active); set { EnsureAttached(); _active = value; } }

    public string ADDomain { get => Read(_activeDirectoryDomain); set { EnsureAttached(); _activeDirectoryDomain = value; } }

    public string Address { get => Read(_address); set { EnsureAttached(); _address = value; } }

    public int DomainID { get => Read(_domainId); set { EnsureAttached(); _domainId = value; } }

    public int ID => Read(0);

    public bool IsAD { get => Read(_isActiveDirectoryAccount); set { EnsureAttached(); _isActiveDirectoryAccount = value; } }

    public string Password { get => Read(_password); set { EnsureAttached(); _password = value; } }

    public float Size => Read(0f);

    public string ADUsername { get => Read(_activeDirectoryUsername); set { EnsureAttached(); _activeDirectoryUsername = value; } }

    public IInterfaceMessages Messages => NotImplemented<IInterfaceMessages>();

    public int MaxSize { get => Read(_maxSize); set { EnsureAttached(); _maxSize = value; } }

    public bool VacationMessageIsOn { get => Read(_vacationMessageIsOn); set { EnsureAttached(); _vacationMessageIsOn = value; } }

    public string VacationMessage { get => Read(_vacationMessage); set { EnsureAttached(); _vacationMessage = value; } }

    public string VacationSubject { get => Read(_vacationSubject); set { EnsureAttached(); _vacationSubject = value; } }

    public IInterfaceFetchAccounts FetchAccounts => NotImplemented<IInterfaceFetchAccounts>();

    public ComAdminLevel AdminLevel { get => Read(_adminLevel); set { EnsureAttached(); _adminLevel = value; } }

    public IInterfaceRules Rules => NotImplemented<IInterfaceRules>();

    public IInterfaceIMAPFolders IMAPFolders => NotImplemented<IInterfaceIMAPFolders>();

    public int QuotaUsed => Read(0);

    public bool ForwardEnabled { get => Read(_forwardEnabled); set { EnsureAttached(); _forwardEnabled = value; } }

    public string ForwardAddress { get => Read(_forwardAddress); set { EnsureAttached(); _forwardAddress = value; } }

    public bool ForwardKeepOriginal { get => Read(_forwardKeepOriginal); set { EnsureAttached(); _forwardKeepOriginal = value; } }

    public bool SignatureEnabled { get => Read(_signatureEnabled); set { EnsureAttached(); _signatureEnabled = value; } }

    public string SignaturePlainText { get => Read(_signaturePlainText); set { EnsureAttached(); _signaturePlainText = value; } }

    public string SignatureHTML { get => Read(_signatureHtml); set { EnsureAttached(); _signatureHtml = value; } }

    public object LastLogonTime => Read(_lastLogonTime);

    public bool VacationMessageExpires { get => Read(_vacationMessageExpires); set { EnsureAttached(); _vacationMessageExpires = value; } }

    public string VacationMessageExpiresDate { get => Read(_vacationMessageExpiresDate); set { EnsureAttached(); _vacationMessageExpiresDate = value; } }

    public string PersonFirstName { get => Read(_personFirstName); set { EnsureAttached(); _personFirstName = value; } }

    public string PersonLastName { get => Read(_personLastName); set { EnsureAttached(); _personLastName = value; } }

    public bool VacationMessageAbortSpamFlagged { get => Read(_vacationMessageAbortSpamFlagged); set { EnsureAttached(); _vacationMessageAbortSpamFlagged = value; } }

    public bool ForwardAbortSpamFlagged { get => Read(_forwardAbortSpamFlagged); set { EnsureAttached(); _forwardAbortSpamFlagged = value; } }

    internal static Account CreateServerAdministrator() =>
        new("Administrator", ComAdminLevel.ServerAdministrator);

    public void Save() => NotImplemented();

    public void DeleteMessages() => NotImplemented();

    public bool ValidatePassword(string password)
    {
        EnsureAttached();
        return false;
    }

    public void UnlockMailbox() => NotImplemented();

    public void Delete() => NotImplemented();

    private T Read<T>(T value)
    {
        EnsureAttached();
        return value;
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
}
