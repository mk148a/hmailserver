using System.Net;
using System.Net.Sockets;
using HMailServer.Core.Abstractions;

namespace HMailServer.Security;

public sealed class SystemLocalHostRuntime : ILocalHostRuntime
{
    private readonly IDnsAddressResolver _addressResolver;
    private readonly ILocalIpAddressProvider _localIpAddressProvider;

    public SystemLocalHostRuntime(
        IDnsAddressResolver addressResolver,
        ILocalIpAddressProvider localIpAddressProvider)
    {
        ArgumentNullException.ThrowIfNull(addressResolver);
        ArgumentNullException.ThrowIfNull(localIpAddressProvider);
        _addressResolver = addressResolver;
        _localIpAddressProvider = localIpAddressProvider;
    }

    public bool IsLocalHost(string hostName)
    {
        if (string.IsNullOrWhiteSpace(hostName))
        {
            return false;
        }

        IPAddress? candidate;
        if (IPAddress.TryParse(hostName, out var literalAddress))
        {
            if (literalAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                return false;
            }

            candidate = literalAddress;
        }
        else
        {
            try
            {
                candidate = _addressResolver
                    .ResolveAddressesAsync(hostName, CancellationToken.None)
                    .AsTask()
                    .GetAwaiter()
                    .GetResult()
                    .FirstOrDefault(static address =>
                        address.AddressFamily == AddressFamily.InterNetwork);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (SocketException)
            {
                return false;
            }

            if (candidate is null)
            {
                return false;
            }
        }

        return _localIpAddressProvider
            .GetLocalIPv4Addresses()
            .Contains(candidate);
    }
}
