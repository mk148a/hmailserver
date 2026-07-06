using System.Net;

namespace HMailServer.Security;

public interface ILocalIpAddressProvider
{
    IReadOnlyList<IPAddress> GetLocalIPv4Addresses();
}
