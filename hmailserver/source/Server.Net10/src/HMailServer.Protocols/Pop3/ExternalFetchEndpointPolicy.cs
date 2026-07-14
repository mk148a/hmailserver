using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace HMailServer.Protocols.Pop3;

public sealed record ExternalFetchEndpointDecision(
    IPAddress Endpoint,
    bool IsAllowed,
    string Reason);

public static class ExternalFetchEndpointPolicy
{
    public static ExternalFetchEndpointDecision Evaluate(
        string hostName,
        IReadOnlyList<IPAddress> resolvedAddresses,
        IReadOnlyList<string>? allowedPrivateCidrs = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostName);
        ArgumentNullException.ThrowIfNull(resolvedAddresses);

        if (resolvedAddresses.Count == 0)
        {
            throw new InvalidOperationException("External fetch destination resolution returned no addresses.");
        }

        var addresses = resolvedAddresses.Select(Normalize).ToArray();
        var explicitLocalHost = IsExplicitLocalHost(hostName);
        if (explicitLocalHost && addresses.Any(static address => !IPAddress.IsLoopback(address)))
        {
            return Denied(addresses[0], "explicit local destination returned a non-loopback address");
        }

        if (explicitLocalHost && addresses.All(static address => IPAddress.IsLoopback(address)))
        {
            return addresses.All(address => IsAllowedByCidrs(address, allowedPrivateCidrs))
                ? Allowed(addresses[0])
                : Denied(addresses[0], "loopback destination requires an explicit CIDR allow-list entry");
        }

        if (addresses.Any(IsMetadataAddress))
        {
            return Denied(addresses[0], "metadata or cloud-platform destination");
        }

        if (addresses.Any(IsSpecialUse))
        {
            if (addresses.All(IsPrivateAddress) &&
                addresses.All(address => IsAllowedByCidrs(address, allowedPrivateCidrs)))
            {
                return Allowed(addresses[0]);
            }

            return Denied(addresses[0], "special-use destination or mixed DNS answer");
        }

        return Allowed(addresses[0]);
    }

    public static IPAddress SelectEndpoint(
        string hostName,
        IReadOnlyList<IPAddress> resolvedAddresses)
    {
        var decision = Evaluate(hostName, resolvedAddresses);
        if (!decision.IsAllowed)
        {
            throw Denied(decision.Reason);
        }

        return decision.Endpoint;
    }

    private static IPAddress Normalize(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return address.IsIPv4MappedToIPv6
            ? address.MapToIPv4()
            : address;
    }

    private static bool IsExplicitLocalHost(string hostName)
    {
        if (hostName.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(hostName, out var address) && IPAddress.IsLoopback(address);
    }

    private static bool IsMetadataAddress(IPAddress address)
    {
        return address.AddressFamily == AddressFamily.InterNetwork
            && (address.Equals(IPAddress.Parse("169.254.169.254"))
                || address.Equals(IPAddress.Parse("168.63.129.16")))
            || address.Equals(IPAddress.Parse("fd00:ec2::254"));
    }

    private static bool IsSpecialUse(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6None))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var value = BinaryPrimitives.ReadUInt32BigEndian(address.GetAddressBytes());
            if (value is 0xC0000009 or 0xC000000A)
            {
                return false;
            }

            return IsInRange(value, 0x00000000, 8)
                || IsInRange(value, 0x0A000000, 8)
                || IsInRange(value, 0x64400000, 10)
                || IsInRange(value, 0xA9FE0000, 16)
                || IsInRange(value, 0xAC100000, 12)
                || IsInRange(value, 0xC0000000, 24)
                || IsInRange(value, 0xC0000200, 24)
                || IsInRange(value, 0xC0A80000, 16)
                || IsInRange(value, 0xC6120000, 15)
                || IsInRange(value, 0xC6336400, 24)
                || IsInRange(value, 0xCB007100, 24)
                || IsInRange(value, 0xC0586300, 24)
                || IsInRange(value, 0xE0000000, 4)
                || IsInRange(value, 0xF0000000, 4)
                || value == 0xFFFFFFFF;
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return true;
        }

        var bytes = address.GetAddressBytes();
        return address.IsIPv6LinkLocal
            || address.IsIPv6SiteLocal
            || address.IsIPv6Multicast
            || (bytes[0] & 0xFE) == 0xFC
            || IsIpv6Prefix(address, "64:ff9b::", 96)
            || IsIpv6Prefix(address, "64:ff9b:1::", 48)
            || IsIpv6Prefix(address, "100::", 64)
            || IsIpv6Prefix(address, "100:0:0:1::", 64)
            || IsIpv6Prefix(address, "2001::", 32)
            || IsIpv6Prefix(address, "2001:2::", 48)
            || IsIpv6Prefix(address, "2001:10::", 28)
            || IsIpv6Prefix(address, "2001:20::", 28)
            || IsIpv6Prefix(address, "2001:30::", 28)
            || IsIpv6Prefix(address, "2001:db8::", 32)
            || IsIpv6Prefix(address, "2002::", 16)
            || IsIpv6Prefix(address, "3fff::", 20)
            || IsIpv6Prefix(address, "5f00::", 16);
    }

    private static bool IsPrivateAddress(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return (address.GetAddressBytes()[0] & 0xFE) == 0xFC;
        }

        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var value = BinaryPrimitives.ReadUInt32BigEndian(address.GetAddressBytes());
        return IsInRange(value, 0x0A000000, 8)
            || IsInRange(value, 0xAC100000, 12)
            || IsInRange(value, 0xC0A80000, 16);
    }

    private static bool IsAllowedByCidrs(IPAddress address, IReadOnlyList<string>? allowedPrivateCidrs)
    {
        if (allowedPrivateCidrs is null)
        {
            return false;
        }

        return allowedPrivateCidrs.Any(cidr => IsContainedByCidr(address, cidr));
    }

    private static bool IsContainedByCidr(IPAddress address, string cidr)
    {
        var separator = cidr.LastIndexOf('/');
        if (separator <= 0 || separator == cidr.Length - 1 ||
            !IPAddress.TryParse(cidr[..separator], out var network) ||
            !int.TryParse(cidr[(separator + 1)..], out var prefixLength))
        {
            return false;
        }

        network = Normalize(network);
        address = Normalize(address);
        var maximumPrefixLength = network.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
        if (network.AddressFamily != address.AddressFamily || prefixLength < 0 || prefixLength > maximumPrefixLength)
        {
            return false;
        }

        var networkBytes = network.GetAddressBytes();
        var addressBytes = address.GetAddressBytes();
        var wholeBytes = prefixLength / 8;
        if (!networkBytes.AsSpan(0, wholeBytes).SequenceEqual(addressBytes.AsSpan(0, wholeBytes)))
        {
            return false;
        }

        var remainingBits = prefixLength % 8;
        return remainingBits == 0 ||
            (networkBytes[wholeBytes] & (byte)(0xFF << (8 - remainingBits))) ==
            (addressBytes[wholeBytes] & (byte)(0xFF << (8 - remainingBits)));
    }

    private static bool IsIpv6Prefix(IPAddress address, string prefix, int prefixLength) =>
        IsContainedByCidr(address, prefix + "/" + prefixLength.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static bool IsInRange(uint value, uint network, int prefixLength)
    {
        var mask = prefixLength == 0
            ? 0u
            : uint.MaxValue << (32 - prefixLength);
        return (value & mask) == network;
    }

    private static ExternalFetchEndpointDecision Allowed(IPAddress endpoint) =>
        new(endpoint, true, string.Empty);

    private static ExternalFetchEndpointDecision Denied(IPAddress endpoint, string reason) =>
        new(endpoint, false, reason);

    private static InvalidOperationException Denied(string reason) =>
        new("External fetch destination was denied by the egress policy: " + reason + ".");
}
