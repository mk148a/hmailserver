namespace HMailServer.Core.Abstractions;

public interface IMessageAntivirusScanner
{
    ValueTask<MessageAntivirusScanResult> ScanAsync(
        ReadOnlyMemory<byte> messageData,
        CancellationToken cancellationToken);
}
