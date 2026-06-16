namespace HMailServer.Core.Abstractions;

public sealed record ImapAclListResult(
    ImapAclCommandStatus Status,
    string MailboxName,
    IReadOnlyList<ImapAclEntry> Entries)
{
    public static ImapAclListResult Failure(ImapAclCommandStatus status) =>
        new(status, string.Empty, Array.Empty<ImapAclEntry>());
}
