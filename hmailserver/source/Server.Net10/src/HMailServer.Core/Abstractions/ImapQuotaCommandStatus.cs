namespace HMailServer.Core.Abstractions;

public enum ImapQuotaCommandStatus
{
    Success,
    QuotaDisabled,
    AccountNotFound,
    QuotaRootNotFound,
    PermissionDenied
}
