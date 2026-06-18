using System.Net;

namespace HMailServer.Core.Abstractions;

public interface IAutoBanLogonFailureRecorder
{
    ValueTask<AutoBanLogonFailureResult> RecordFailureAsync(
        IPAddress clientAddress,
        string username,
        CancellationToken cancellationToken);

    ValueTask ClearOldFailuresAsync(CancellationToken cancellationToken);
}
