using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(false)]
public static class LegacyLocalScannerTargetGuard
{
    private static readonly Lazy<HashSet<IPAddress>> _localInterfaceAddresses =
        new(EnumerateLocalInterfaceAddresses);

    public static bool IsLocalTarget(string hostname)
    {
        return TryGetValidatedLocalAddress(hostname, out _);
    }

    public static bool TryGetValidatedLocalAddress(string hostname, out IPAddress address)
    {
        address = IPAddress.None;
        if (string.IsNullOrWhiteSpace(hostname))
        {
            return false;
        }

        IReadOnlyList<IPAddress> addresses;
        if (IPAddress.TryParse(hostname, out var literal))
        {
            addresses = new[] { literal };
        }
        else
        {
            try
            {
                addresses = Dns.GetHostAddresses(hostname);
            }
            catch (SocketException)
            {
                return false;
            }
        }

        if (addresses.Count == 0 || !addresses.All(IsLocalAddress))
        {
            return false;
        }

        address = addresses[0];
        return true;
    }

    public static bool IsLocalAddress(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var normalized = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        if (IPAddress.IsLoopback(normalized) || normalized.Equals(IPAddress.Any) || normalized.Equals(IPAddress.IPv6Any))
        {
            return true;
        }

        return _localInterfaceAddresses.Value.Contains(normalized);
    }

    private static HashSet<IPAddress> EnumerateLocalInterfaceAddresses()
    {
        var addresses = new HashSet<IPAddress>();
        try
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                foreach (var unicast in networkInterface.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address is not null)
                    {
                        var normalized = unicast.Address.IsIPv4MappedToIPv6
                            ? unicast.Address.MapToIPv4()
                            : unicast.Address;
                        addresses.Add(normalized);
                    }
                }
            }
        }
        catch (NetworkInformationException)
        {
        }

        return addresses;
    }
}
