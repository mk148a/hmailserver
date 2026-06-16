namespace HMailServer.Core.Abstractions;

public enum SmtpRuleActionType
{
    Unknown = 0,
    Delete = 1,
    Forward = 2,
    Reply = 3,
    MoveToImapFolder = 4,
    ScriptFunction = 5,
    StopRuleProcessing = 6,
    SetHeaderValue = 7,
    SendUsingRoute = 8,
    CreateCopy = 9,
    BindToAddress = 10
}
