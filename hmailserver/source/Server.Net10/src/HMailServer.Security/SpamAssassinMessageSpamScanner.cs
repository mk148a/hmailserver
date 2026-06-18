using HMailServer.Core.Abstractions;

namespace HMailServer.Security;

public sealed class SpamAssassinMessageSpamScanner : IMessageSpamScanner
{
    private readonly SpamAssassinClient _client;

    public SpamAssassinMessageSpamScanner(SpamAssassinClient client)
    {
        _client = client;
    }

    public async ValueTask<MessageSpamScanResult> ScanAsync(
        ReadOnlyMemory<byte> messageData,
        string envelopeFrom,
        CancellationToken cancellationToken)
    {
        var result = await _client
            .ProcessAsync(messageData, envelopeFrom, cancellationToken)
            .ConfigureAwait(false);

        return new MessageSpamScanResult(
            result.Succeeded,
            result.IsSpam,
            result.Score,
            result.Details,
            result.MessageData);
    }
}
