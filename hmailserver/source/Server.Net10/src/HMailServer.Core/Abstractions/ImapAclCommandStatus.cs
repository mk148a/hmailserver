namespace HMailServer.Core.Abstractions;

public enum ImapAclCommandStatus
{
    Success,
    AclDisabled,
    MailboxNotFound,
    PermissionDenied,
    PrivateMailboxNotSupported,
    IdentifierNotFound
}
