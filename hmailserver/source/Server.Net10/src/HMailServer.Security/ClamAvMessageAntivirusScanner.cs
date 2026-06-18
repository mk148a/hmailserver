using HMailServer.Core.Abstractions;

namespace HMailServer.Security;

public sealed class ClamAvMessageAntivirusScanner : IMessageAntivirusScanner
{
    private readonly ClamAvInstreamClient _client;

    public ClamAvMessageAntivirusScanner(ClamAvInstreamClient client)
    {
        _client = client;
    }

    public async ValueTask<MessageAntivirusScanResult> ScanAsync(
        ReadOnlyMemory<byte> messageData,
        CancellationToken cancellationToken)
    {
        var result = await _client.ScanAsync(messageData, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return MessageAntivirusScanResult.Error(result.Details);
        }

        return result.IsInfected
            ? MessageAntivirusScanResult.Infected(result.VirusName, result.Details)
            : MessageAntivirusScanResult.Clean(result.Details);
    }
}
