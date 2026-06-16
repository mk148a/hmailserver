using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public sealed record ImapCopyCommand(
    IReadOnlyList<ImapIdRange> MessageSet,
    string DestinationMailbox);
