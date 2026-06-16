namespace HMailServer.Core.Abstractions;

public sealed record ImapAclEntry(
    string Identifier,
    string Rights);
