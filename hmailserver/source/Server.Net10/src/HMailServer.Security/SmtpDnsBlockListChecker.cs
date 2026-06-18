using System.Net;
using System.Net.Sockets;
using HMailServer.Core.Abstractions;

namespace HMailServer.Security;

public sealed class SmtpDnsBlockListChecker : ISmtpDnsBlockListChecker
{
    private readonly IDnsAddressResolver _resolver;
    private readonly SmtpDnsBlockListOptions _options;
    private readonly string[] _zones;

    public SmtpDnsBlockListChecker(
        IDnsAddressResolver resolver,
        SmtpDnsBlockListOptions? options = null)
    {
        _resolver = resolver;
        _options = options ?? new SmtpDnsBlockListOptions();
        _zones = _options.Zones
            .Select(NormalizeZone)
            .Where(static zone => zone.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async ValueTask<SmtpDnsBlockListResult> CheckAsync(
        SmtpReceiveRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Enabled
            || _zones.Length == 0
            || (_options.SkipAuthenticated && request.IsAuthenticated)
            || !IPAddress.TryParse(request.ClientIPAddress, out var clientAddress))
        {
            return SmtpDnsBlockListResult.NotListed;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_options.Timeout > TimeSpan.Zero)
        {
            timeout.CancelAfter(_options.Timeout);
        }

        foreach (var zone in _zones)
        {
            var queryHost = BuildQueryHost(clientAddress, zone);
            if (queryHost.Length == 0)
            {
                continue;
            }

            IReadOnlyList<IPAddress> responseAddresses;
            try
            {
                responseAddresses = await _resolver
                    .ResolveAddressesAsync(queryHost, timeout.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return SmtpDnsBlockListResult.NotListed;
            }
            catch (SocketException)
            {
                continue;
            }
            catch
            {
                continue;
            }

            if (responseAddresses.Count > 0)
            {
                var responseAddress = responseAddresses[0].ToString();
                return SmtpDnsBlockListResult.Blocked(
                    zone,
                    queryHost,
                    responseAddress,
                    BuildFailureResponse(zone, queryHost, responseAddress));
            }
        }

        return SmtpDnsBlockListResult.NotListed;
    }

    private string BuildFailureResponse(
        string listHost,
        string queryHost,
        string responseAddress)
    {
        var message = _options.RejectionMessageTemplate
            .Replace("{ListHost}", listHost, StringComparison.Ordinal)
            .Replace("{QueryHost}", queryHost, StringComparison.Ordinal)
            .Replace("{ResponseAddress}", responseAddress, StringComparison.Ordinal);
        message = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (message.Length == 0)
        {
            return "554 Rejected by DNS blocklist";
        }

        return StartsWithSmtpReplyCode(message)
            ? message
            : "554 " + message;
    }

    private static string BuildQueryHost(IPAddress address, string zone)
    {
        var bytes = address.GetAddressBytes();
        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => string.Join('.', bytes.Reverse()) + "." + zone,
            AddressFamily.InterNetworkV6 => BuildIpv6NibbleHost(bytes) + "." + zone,
            _ => string.Empty
        };
    }

    private static string BuildIpv6NibbleHost(byte[] bytes)
    {
        var nibbles = new List<char>(bytes.Length * 2);
        for (var index = bytes.Length - 1; index >= 0; index--)
        {
            var value = bytes[index];
            nibbles.Add(ToHexNibble(value & 0x0F));
            nibbles.Add(ToHexNibble(value >> 4));
        }

        return string.Join('.', nibbles);
    }

    private static char ToHexNibble(int value) =>
        (char)(value < 10 ? '0' + value : 'a' + value - 10);

    private static string NormalizeZone(string zone) =>
        zone.Trim().TrimEnd('.');

    private static bool StartsWithSmtpReplyCode(string value) =>
        value.Length >= 4
        && char.IsDigit(value[0])
        && char.IsDigit(value[1])
        && char.IsDigit(value[2])
        && value[3] == ' ';
}
