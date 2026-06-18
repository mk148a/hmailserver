namespace HMailServer.Core.Abstractions;

public sealed record MessageSpamPolicyResult(
    byte[] MessageData,
    bool MarkAsSpam);
