namespace HMailServer.Storage.SqlServer;

public interface ISmtpQueueWriter
{
    ValueTask EnqueueAsync(
        SmtpQueueWriteRequest request,
        CancellationToken cancellationToken);
}
