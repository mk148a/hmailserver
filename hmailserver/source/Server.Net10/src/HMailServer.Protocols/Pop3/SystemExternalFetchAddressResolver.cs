using System.Net;
using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Pop3;

internal sealed class SystemExternalFetchAddressResolver : IExternalFetchAddressResolver
{
    public ValueTask<IReadOnlyList<IPAddress>> ResolveAddressesAsync(
        string hostName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostName);

        if (IPAddress.TryParse(hostName, out var address))
        {
            return ValueTask.FromResult<IReadOnlyList<IPAddress>>([address]);
        }

        return ResolveHostNameAsync(hostName, cancellationToken);
    }

    private static async ValueTask<IReadOnlyList<IPAddress>> ResolveHostNameAsync(
        string hostName,
        CancellationToken cancellationToken)
    {
        var addresses = await Dns
            .GetHostAddressesAsync(hostName)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        return addresses;
    }
}
