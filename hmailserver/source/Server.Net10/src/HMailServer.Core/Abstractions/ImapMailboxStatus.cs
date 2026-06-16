namespace HMailServer.Core.Abstractions;

public sealed record ImapMailboxStatus(
    string MailboxName,
    IReadOnlyDictionary<ImapStatusItem, long> Values);
