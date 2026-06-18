using System.Net;

namespace HMailServer.Security;

public sealed class SystemDnsAddressResolver : IDnsAddressResolver
{
    public async ValueTask<IReadOnlyList<IPAddress>> ResolveAddressesAsync(
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
