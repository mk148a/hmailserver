namespace HMailServer.Core.Abstractions;

public sealed record MessageSpamPolicyResult(
    byte[] MessageData,
    bool MarkAsSpam,
    bool RejectMessage = false,
    string FailureResponse = "");
