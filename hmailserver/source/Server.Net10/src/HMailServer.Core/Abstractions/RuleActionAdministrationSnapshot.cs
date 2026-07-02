namespace HMailServer.Core.Abstractions;

public sealed record RuleActionAdministrationSnapshot(
    int Id,
    int RuleId,
    int Type,
    string Subject,
    string Body,
    string FromName,
    string FromAddress,
    string Filename,
    string To,
    string ImapFolder,
    string ScriptFunction,
    string HeaderName,
    string Value,
    int RouteId,
    bool AbortSpamFlagged,
    int SortOrder);
