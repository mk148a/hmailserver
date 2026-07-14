using System.Net;

namespace HMailServer.Core.Abstractions;

public interface IExternalFetchAddressResolver
{
    ValueTask<IReadOnlyList<IPAddress>> ResolveAddressesAsync(
        string hostName,
        CancellationToken cancellationToken);
}
