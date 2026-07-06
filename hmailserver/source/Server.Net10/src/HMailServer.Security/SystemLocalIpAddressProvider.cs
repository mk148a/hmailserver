using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace HMailServer.Security;

public sealed class SystemLocalIpAddressProvider : ILocalIpAddressProvider
{
    public IReadOnlyList<IPAddress> GetLocalIPv4Addresses()
    {
        var addresses = new HashSet<IPAddress>();
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            foreach (var unicastAddress in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    addresses.Add(unicastAddress.Address);
                }
            }
        }

        return addresses.ToArray();
    }
}
