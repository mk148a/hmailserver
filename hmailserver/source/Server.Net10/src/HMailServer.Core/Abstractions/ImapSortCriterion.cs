namespace HMailServer.Core.Abstractions;

public sealed record ImapSortCriterion(
    ImapSortKey Key,
    bool Descending);
