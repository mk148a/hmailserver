using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public sealed record ImapStatusCommand(
    string MailboxName,
    IReadOnlyList<ImapStatusItem> Items);
