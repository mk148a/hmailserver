namespace HMailServer.Core.Abstractions;

public sealed record SmtpRuleProcessingResult(
    bool Accepted,
    string? FailureResponse,
    byte[] MessageData,
    bool DropMessage,
    string? MoveToImapFolder,
    IReadOnlyList<SmtpRuleGeneratedMessage> GeneratedMessages,
    int ForcedRouteId = 0,
    string? BindToAddress = null,
    IReadOnlyList<ScriptMessageCopyOperation>? MessageCopyOperations = null)
{
    public static SmtpRuleProcessingResult Continue(byte[] messageData) =>
        new(Accepted: true, FailureResponse: null, messageData, DropMessage: false, MoveToImapFolder: null, GeneratedMessages: []);

    public static SmtpRuleProcessingResult Continue(
        byte[] messageData,
        string? moveToImapFolder,
        IReadOnlyList<SmtpRuleGeneratedMessage>? generatedMessages = null,
        int forcedRouteId = 0,
        string? bindToAddress = null,
        IReadOnlyList<ScriptMessageCopyOperation>? messageCopyOperations = null) =>
        new(
            Accepted: true,
            FailureResponse: null,
            messageData,
            DropMessage: false,
            moveToImapFolder,
            generatedMessages ?? [],
            forcedRouteId,
            bindToAddress,
            messageCopyOperations);

    public static SmtpRuleProcessingResult Drop(
        byte[] messageData,
        IReadOnlyList<SmtpRuleGeneratedMessage>? generatedMessages = null,
        IReadOnlyList<ScriptMessageCopyOperation>? messageCopyOperations = null) =>
        new(
            Accepted: true,
            FailureResponse: null,
            messageData,
            DropMessage: true,
            MoveToImapFolder: null,
            GeneratedMessages: generatedMessages ?? [],
            MessageCopyOperations: messageCopyOperations);

    public static SmtpRuleProcessingResult Failure(
        string response,
        byte[] messageData) =>
        new(Accepted: false, FailureResponse: response, messageData, DropMessage: false, MoveToImapFolder: null, GeneratedMessages: []);
}
