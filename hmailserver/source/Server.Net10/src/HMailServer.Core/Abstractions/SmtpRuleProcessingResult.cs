namespace HMailServer.Core.Abstractions;

public sealed record SmtpRuleProcessingResult(
    bool Accepted,
    string? FailureResponse,
    byte[] MessageData,
    bool DropMessage,
    string? MoveToImapFolder,
    IReadOnlyList<SmtpRuleGeneratedMessage> GeneratedMessages)
{
    public static SmtpRuleProcessingResult Continue(byte[] messageData) =>
        new(Accepted: true, FailureResponse: null, messageData, DropMessage: false, MoveToImapFolder: null, GeneratedMessages: []);

    public static SmtpRuleProcessingResult Continue(
        byte[] messageData,
        string? moveToImapFolder,
        IReadOnlyList<SmtpRuleGeneratedMessage>? generatedMessages = null) =>
        new(
            Accepted: true,
            FailureResponse: null,
            messageData,
            DropMessage: false,
            moveToImapFolder,
            generatedMessages ?? []);

    public static SmtpRuleProcessingResult Drop(byte[] messageData) =>
        new(Accepted: true, FailureResponse: null, messageData, DropMessage: true, MoveToImapFolder: null, GeneratedMessages: []);

    public static SmtpRuleProcessingResult Failure(
        string response,
        byte[] messageData) =>
        new(Accepted: false, FailureResponse: response, messageData, DropMessage: false, MoveToImapFolder: null, GeneratedMessages: []);
}
