using System.Net;
using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols;

internal static class AutoBanLogonFailureRecorderExtensions
{
    public static async ValueTask<bool> TryRecordFailureAsync(
        this IAutoBanLogonFailureRecorder? recorder,
        string clientIpAddress,
        string username,
        CancellationToken cancellationToken)
    {
        if (recorder is null ||
            !IPAddress.TryParse(clientIpAddress, out var clientAddress))
        {
            return false;
        }

        try
        {
            var result = await recorder
                .RecordFailureAsync(clientAddress, username, cancellationToken)
                .ConfigureAwait(false);
            return result.Disconnect;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
