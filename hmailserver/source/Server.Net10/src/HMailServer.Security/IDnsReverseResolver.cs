using System.Net;

namespace HMailServer.Security;

public interface IDnsReverseResolver
{
    ValueTask<IReadOnlyList<string>> ResolveHostNamesAsync(
        IPAddress address,
        CancellationToken cancellationToken);
}
