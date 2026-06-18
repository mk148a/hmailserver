using System.Net;

namespace HMailServer.Security;

public interface IDnsAddressResolver
{
    ValueTask<IReadOnlyList<IPAddress>> ResolveAddressesAsync(
        string hostName,
        CancellationToken cancellationToken);
}
