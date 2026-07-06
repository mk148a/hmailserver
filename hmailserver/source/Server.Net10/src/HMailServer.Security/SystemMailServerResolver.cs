using System.Net;
using System.Net.Sockets;
using HMailServer.Core.Abstractions;

namespace HMailServer.Security;

public sealed class SystemMailServerResolver : IMailServerResolver
{
    private const int MaximumCnameRecursion = 10;

    private readonly IMailServerDnsResolver _dnsResolver;
    private readonly bool _ipv6Available;

    public SystemMailServerResolver(IMailServerDnsResolver dnsResolver)
        : this(dnsResolver, Socket.OSSupportsIPv6)
    {
    }

    internal SystemMailServerResolver(
        IMailServerDnsResolver dnsResolver,
        bool ipv6Available)
    {
        ArgumentNullException.ThrowIfNull(dnsResolver);
        _dnsResolver = dnsResolver;
        _ipv6Available = ipv6Available;
    }

    public string GetMailServer(string emailAddress)
    {
        emailAddress ??= string.Empty;
        var domainName = emailAddress[(emailAddress.LastIndexOf('@') + 1)..];
        if (domainName.Length == 0)
        {
            return string.Empty;
        }

        var addresses = ResolveDomainAsync(domainName, recursionLevel: 0)
            .GetAwaiter()
            .GetResult();
        var uniqueAddresses = new List<string>(addresses.Count);
        var seenAddresses = new HashSet<string>(StringComparer.Ordinal);
        foreach (var address in addresses)
        {
            if (seenAddresses.Add(address))
            {
                uniqueAddresses.Add(address);
            }
        }

        return string.Join(',', uniqueAddresses);
    }

    private async Task<IReadOnlyList<string>> ResolveDomainAsync(
        string domainName,
        int recursionLevel)
    {
        if (recursionLevel > MaximumCnameRecursion || domainName.Length == 0)
        {
            return Array.Empty<string>();
        }

        var mxResponse = await _dnsResolver
            .QueryMailServerMxAsync(domainName, CancellationToken.None)
            .ConfigureAwait(false);
        if (mxResponse.Status == MailServerDnsStatus.Success)
        {
            if (mxResponse.Records.Any(static record =>
                    record.Preference == 0 && record.Exchange == "."))
            {
                return Array.Empty<string>();
            }

            var addresses = new List<string>();
            foreach (var mxHost in mxResponse.Records)
            {
                addresses.AddRange(
                    await ResolveHostAddressesAsync(mxHost.Exchange, recursionLevel: 0)
                        .ConfigureAwait(false));
            }

            return addresses;
        }

        if (mxResponse.Status != MailServerDnsStatus.NoData)
        {
            return Array.Empty<string>();
        }

        var cnameResponse = await _dnsResolver
            .QueryMailServerCnameAsync(domainName, CancellationToken.None)
            .ConfigureAwait(false);
        if (cnameResponse.Status == MailServerDnsStatus.Success
            && cnameResponse.Records.Count == 1)
        {
            return await ResolveDomainAsync(
                    cnameResponse.Records[0],
                    recursionLevel + 1)
                .ConfigureAwait(false);
        }

        return await ResolveHostAddressesAsync(domainName, recursionLevel: 0)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<string>> ResolveHostAddressesAsync(
        string hostName,
        int recursionLevel)
    {
        if (recursionLevel > MaximumCnameRecursion || hostName.Length == 0)
        {
            return Array.Empty<string>();
        }

        var normalizedHostName = hostName.TrimEnd('.');
        if (IPAddress.TryParse(normalizedHostName, out _))
        {
            return [normalizedHostName];
        }

        var addresses = new List<string>();
        await AppendAddressesAsync(
                hostName,
                AddressFamily.InterNetwork,
                addresses)
            .ConfigureAwait(false);
        if (_ipv6Available)
        {
            await AppendAddressesAsync(
                    hostName,
                    AddressFamily.InterNetworkV6,
                    addresses)
                .ConfigureAwait(false);
        }
        if (addresses.Count > 0)
        {
            return addresses;
        }

        var cnameResponse = await _dnsResolver
            .QueryMailServerCnameAsync(hostName, CancellationToken.None)
            .ConfigureAwait(false);
        return cnameResponse.Status == MailServerDnsStatus.Success
            && cnameResponse.Records.Count == 1
            ? await ResolveHostAddressesAsync(
                    cnameResponse.Records[0],
                    recursionLevel + 1)
                .ConfigureAwait(false)
            : Array.Empty<string>();
    }

    private async Task AppendAddressesAsync(
        string hostName,
        AddressFamily addressFamily,
        List<string> addresses)
    {
        var response = await _dnsResolver
            .QueryMailServerAddressesAsync(
                hostName,
                addressFamily,
                CancellationToken.None)
            .ConfigureAwait(false);
        if (response.Status == MailServerDnsStatus.Success)
        {
            addresses.AddRange(response.Records.Select(static address => address.ToString()));
        }
    }
}
