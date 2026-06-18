namespace HMailServer.Core.Abstractions;

public sealed record MessageSpamScanResult(
    bool Succeeded,
    bool IsSpam,
    int Score,
    string Details,
    byte[] MessageData)
{
    public static MessageSpamScanResult Clean(
        byte[] messageData,
        string details = "",
        int score = 0) =>
        new(
            Succeeded: true,
            IsSpam: false,
            score,
            details,
            messageData);

    public static MessageSpamScanResult Spam(
        byte[] messageData,
        int score,
        string details = "") =>
        new(
            Succeeded: true,
            IsSpam: true,
            score,
            details,
            messageData);

    public static MessageSpamScanResult Error(
        byte[] originalMessageData,
        string details) =>
        new(
            Succeeded: false,
            IsSpam: false,
            Score: 0,
            details,
            originalMessageData);
}
