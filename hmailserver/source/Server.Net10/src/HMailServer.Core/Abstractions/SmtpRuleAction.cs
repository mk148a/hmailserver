namespace HMailServer.Core.Abstractions;

public sealed record SmtpRuleAction(
    long Id,
    SmtpRuleActionType Type,
    int SortOrder,
    string ImapFolder,
    string Subject,
    string FromName,
    string FromAddress,
    string To,
    string Body,
    string FileName,
    string ScriptFunction,
    string HeaderName,
    string Value,
    long RouteId,
    bool AbortSpamFlagged);
