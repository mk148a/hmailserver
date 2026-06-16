namespace HMailServer.Storage.SqlServer;

public sealed record DeliveryBounceOptions(
    string MailerDaemonAddress,
    string Subject)
{
    public static DeliveryBounceOptions Default(string serverName) =>
        new(
            string.IsNullOrWhiteSpace(serverName)
                ? "MAILER-DAEMON@localhost"
                : "MAILER-DAEMON@" + serverName,
            "Undeliverable: Message delivery failed");
}
