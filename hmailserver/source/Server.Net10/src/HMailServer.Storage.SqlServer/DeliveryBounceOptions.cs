namespace HMailServer.Storage.SqlServer;

public sealed record DeliveryBounceOptions(
    string ServerName,
    string MailerDaemonAddress,
    string SubjectTemplate,
    string BodyTemplate,
    int MaxFailureDescriptionLength)
{
    public const string DefaultBodyTemplate = """
Your message could not be delivered.

Server: {ServerName}
Original queue message id: {MessageId}
Original sender: {Sender}
Original file: {FileName}
Original size: {Size}
Original date (UTC): {CreatedUtc}
Delivery attempt: {RetryCount}

Failed recipients:
{Recipients}

Reason:
{FailureDescription}
""";

    public static DeliveryBounceOptions Default(string serverName) =>
        new(
            NormalizeServerName(serverName),
            "MAILER-DAEMON@" + NormalizeServerName(serverName),
            "Undeliverable: message {MessageId}",
            DefaultBodyTemplate,
            MaxFailureDescriptionLength: 4096);

    private static string NormalizeServerName(string serverName) =>
        string.IsNullOrWhiteSpace(serverName)
            ? "localhost"
            : serverName;
}
