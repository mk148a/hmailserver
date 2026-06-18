namespace HMailServer.Core.Abstractions;

public interface IMessageSpamScanner
{
    ValueTask<MessageSpamScanResult> ScanAsync(
        ReadOnlyMemory<byte> messageData,
        string envelopeFrom,
        CancellationToken cancellationToken);
}
