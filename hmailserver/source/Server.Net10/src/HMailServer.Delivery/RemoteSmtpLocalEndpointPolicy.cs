using System.Net;
using System.Net.NetworkInformation;
using HMailServer.Core.Abstractions;

namespace HMailServer.Delivery;

public sealed class RemoteSmtpLocalEndpointPolicy
{
    private readonly Func<IReadOnlyList<IPEndPoint>> _listenersProvider;
    private readonly Func<IReadOnlyList<IPAddress>> _localAddressesProvider;

    public RemoteSmtpLocalEndpointPolicy(
        Func<IReadOnlyList<IPEndPoint>>? listenersProvider = null,
        Func<IReadOnlyList<IPAddress>>? localAddressesProvider = null)
    {
        _listenersProvider = listenersProvider ?? GetActiveTcpListeners;
        _localAddressesProvider = localAddressesProvider ?? GetLocalAddresses;
    }

    public void EnsureAllowed(RemoteSmtpEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        if (!endpoint.EnforceLocalEndpointGuard)
        {
            return;
        }

        if (!IPAddress.TryParse(endpoint.ConnectionAddress, out var address))
        {
            throw new InvalidOperationException(
                "DNS-derived SMTP delivery requires an explicit connection address.");
        }

        if (_listenersProvider().Any(listener =>
                listener.Port == endpoint.Port
                && MatchesAddress(listener.Address, address, _localAddressesProvider())))
        {
            throw new RemoteSmtpLocalEndpointDeniedException(
                $"SMTP delivery to local listening endpoint '{address}:{endpoint.Port}' is not allowed.");
        }
    }

    private static bool MatchesAddress(
        IPAddress listenerAddress,
        IPAddress remoteAddress,
        IReadOnlyList<IPAddress> localAddresses)
    {
        listenerAddress = NormalizeAddress(listenerAddress);
        remoteAddress = NormalizeAddress(remoteAddress);

        if (listenerAddress.Equals(IPAddress.Any))
        {
            return remoteAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                && IsLocalAddress(remoteAddress, localAddresses);
        }

        if (listenerAddress.Equals(IPAddress.IPv6Any))
        {
            return remoteAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                && IsLocalAddress(remoteAddress, localAddresses);
        }

        return listenerAddress.Equals(remoteAddress);
    }

    private static bool IsLocalAddress(IPAddress address, IReadOnlyList<IPAddress> localAddresses) =>
        IPAddress.IsLoopback(address)
        || localAddresses.Any(localAddress => NormalizeAddress(localAddress).Equals(address));

    private static IPAddress NormalizeAddress(IPAddress address) =>
        address.IsIPv4MappedToIPv6
            ? address.MapToIPv4()
            : address;

    private static IReadOnlyList<IPEndPoint> GetActiveTcpListeners() =>
        IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();

    private static IReadOnlyList<IPAddress> GetLocalAddresses() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .SelectMany(static networkInterface => networkInterface.GetIPProperties().UnicastAddresses)
            .Select(static unicastAddress => unicastAddress.Address)
            .ToArray();
}

public sealed class RemoteSmtpLocalEndpointDeniedException : InvalidOperationException
{
    public RemoteSmtpLocalEndpointDeniedException(string message)
        : base(message)
    {
    }
}
