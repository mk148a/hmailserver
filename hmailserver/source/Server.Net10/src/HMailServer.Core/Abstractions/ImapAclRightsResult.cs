namespace HMailServer.Core.Abstractions;

public sealed record ImapAclRightsResult(
    ImapAclCommandStatus Status,
    string MailboxName,
    string Rights)
{
    public static ImapAclRightsResult Failure(ImapAclCommandStatus status) =>
        new(status, string.Empty, string.Empty);
}
