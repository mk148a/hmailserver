namespace HMailServer.Core.Abstractions;

public sealed record ImapMailboxListEntry(
    string Name,
    bool HasChildren,
    bool IsSelectable,
    bool IsSubscribed);
