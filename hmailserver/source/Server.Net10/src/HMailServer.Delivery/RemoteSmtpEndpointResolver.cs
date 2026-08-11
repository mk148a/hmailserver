using HMailServer.Core.Abstractions;
using HMailServer.Security;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;

namespace HMailServer.Delivery;

public sealed class RemoteSmtpEndpointResolver : IRemoteSmtpEndpointResolver
{
    private readonly IDnsMxResolver _mxResolver;
    private readonly IDnsAddressResolver _addressResolver;
    private readonly RemoteSmtpEndpointResolverOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    public RemoteSmtpEndpointResolver()
        : this(
            new SystemDnsMxResolver(),
            new SystemDnsAddressResolver(),
            RemoteSmtpEndpointResolverOptions.Default,
            TimeProvider.System)
    {
    }

    public RemoteSmtpEndpointResolver(
        IDnsMxResolver mxResolver,
        RemoteSmtpEndpointResolverOptions options)
        : this(mxResolver, new SystemDnsAddressResolver(), options, TimeProvider.System)
    {
    }

    public RemoteSmtpEndpointResolver(
        IDnsAddressResolver addressResolver,
        RemoteSmtpEndpointResolverOptions options)
        : this(new SystemDnsMxResolver(), addressResolver, options, TimeProvider.System)
    {
    }

    public RemoteSmtpEndpointResolver(
        IDnsMxResolver mxResolver,
        IDnsAddressResolver addressResolver,
        RemoteSmtpEndpointResolverOptions options)
        : this(mxResolver, addressResolver, options, TimeProvider.System)
    {
    }

    public RemoteSmtpEndpointResolver(
        IDnsMxResolver mxResolver,
        IDnsAddressResolver addressResolver,
        RemoteSmtpEndpointResolverOptions options,
        TimeProvider timeProvider)
    {
        _mxResolver = mxResolver;
        _addressResolver = addressResolver;
        _options = options;
        _timeProvider = timeProvider;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.DefaultCacheTtl.Ticks, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.NegativeCacheTtl.Ticks, 0);
    }

    public async ValueTask<RemoteSmtpEndpoint> ResolveAsync(
        DeliveryTarget target,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (target.Kind == DeliveryTargetKind.Route)
        {
            if (target.Route is null)
            {
                throw new InvalidOperationException("Route delivery target is missing route metadata.");
            }

            var route = target.Route;
            if (route.RouteId == 0)
            {
                return await ResolveGlobalRelayerAsync(target, route, cancellationToken).ConfigureAwait(false);
            }

            return new RemoteSmtpEndpoint(
                route.TargetHost,
                route.TargetPort <= 0 ? 25 : route.TargetPort,
                (RemoteSmtpConnectionSecurity)route.ConnectionSecurity,
                route.RequiresAuthentication,
                route.AuthenticationUsername,
                route.AuthenticationPassword,
                VerifyRemoteSslCertificate: target.VerifyRemoteSslCertificate);
        }

        if (target.Kind == DeliveryTargetKind.RemoteDomain)
        {
            var mxHosts = await ResolveRemoteHostsAsync(target.DomainName, cancellationToken).ConfigureAwait(false);
            if (target.MaxNumberOfMxHosts > 0)
            {
                mxHosts = mxHosts.Take(target.MaxNumberOfMxHosts).ToArray();
            }

            var mxHost = mxHosts[0];
            var connectionSecurity = target.RemoteConnectionSecurity switch
            {
                (int)RemoteSmtpConnectionSecurity.None => RemoteSmtpConnectionSecurity.None,
                (int)RemoteSmtpConnectionSecurity.Ssl => RemoteSmtpConnectionSecurity.Ssl,
                (int)RemoteSmtpConnectionSecurity.StartTlsOptional => RemoteSmtpConnectionSecurity.StartTlsOptional,
                (int)RemoteSmtpConnectionSecurity.StartTlsRequired => RemoteSmtpConnectionSecurity.StartTlsRequired,
                _ => throw new InvalidOperationException(
                    $"Global SMTP connection security value {target.RemoteConnectionSecurity} is invalid.")
            };
            return new RemoteSmtpEndpoint(
                mxHost,
                Port: 25,
                connectionSecurity,
                VerifyRemoteSslCertificate: target.VerifyRemoteSslCertificate,
                HostCandidates: mxHosts.Count > 1 ? mxHosts : null);
        }

        throw new InvalidOperationException("Local delivery targets do not have remote SMTP endpoints.");
    }

    private async ValueTask<RemoteSmtpEndpoint> ResolveGlobalRelayerAsync(
        DeliveryTarget target,
        SmtpRouteResolution route,
        CancellationToken cancellationToken)
    {
        var hosts = route.TargetHost.Split(
            '|',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var candidates = new List<RemoteSmtpEndpoint>();
        var seenAddresses = new HashSet<IPAddress>();
        Exception? lastResolutionFailure = null;

        foreach (var host in hosts)
        {
            if (IPAddress.TryParse(host, out var configuredAddress))
            {
                AddAddressCandidate(host, configuredAddress, host);
                continue;
            }

            IReadOnlyList<IPAddress> addresses;
            try
            {
                addresses = await _addressResolver
                    .ResolveAddressesAsync(host, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (IOException ex)
            {
                lastResolutionFailure = ex;
                continue;
            }
            catch (SocketException ex)
            {
                lastResolutionFailure = ex;
                continue;
            }
            catch (ArgumentException ex)
            {
                lastResolutionFailure = ex;
                continue;
            }

            foreach (var address in addresses)
            {
                AddAddressCandidate(host, address, address.ToString());
            }
        }

        if (lastResolutionFailure is not null)
        {
            throw new IOException(
                "Global SMTP relayer host resolution failed.",
                lastResolutionFailure);
        }

        if (candidates.Count == 0)
        {
            throw new IOException(
                "Global SMTP relayer host resolution failed.",
                lastResolutionFailure);
        }

        if (target.MaxNumberOfMxHosts > 0)
        {
            candidates = candidates.Take(target.MaxNumberOfMxHosts).ToList();
        }

        var first = candidates[0];
        return first with
        {
            Candidates = candidates.ToArray(),
            VerifyRemoteSslCertificate = target.VerifyRemoteSslCertificate
        };

        void AddAddressCandidate(string host, IPAddress address, string connectionAddress)
        {
            if (!seenAddresses.Add(address))
            {
                return;
            }

            candidates.Add(new RemoteSmtpEndpoint(
                host,
                route.TargetPort <= 0 ? 25 : route.TargetPort,
                (RemoteSmtpConnectionSecurity)route.ConnectionSecurity,
                route.RequiresAuthentication,
                route.AuthenticationUsername,
                route.AuthenticationPassword,
                VerifyRemoteSslCertificate: target.VerifyRemoteSslCertificate,
                ConnectionAddress: connectionAddress));
        }
    }

    private async ValueTask<IReadOnlyList<string>> ResolveRemoteHostsAsync(
        string domainName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domainName);

        var now = _timeProvider.GetUtcNow();
        if (_cache.TryGetValue(domainName, out var cached) && cached.ExpiresUtc > now)
        {
            return cached.Hosts;
        }

        var records = await _mxResolver.ResolveMxAsync(domainName, cancellationToken).ConfigureAwait(false);
        if (records.Any(static record => record.Preference == 0 && record.Exchange == "."))
        {
            throw new IOException("DNS MX lookup returned a null MX record.");
        }

        var hosts = records
            .OrderBy(static record => record.Preference)
            .ThenBy(static record => record.Exchange, StringComparer.OrdinalIgnoreCase)
            .Select(static record => record.Exchange.TrimEnd('.'))
            .Where(static host => !string.IsNullOrWhiteSpace(host))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (hosts.Length == 0)
        {
            hosts = [domainName];
        }

        var ttl = records.Count == 0
            ? _options.NegativeCacheTtl
            : records.Min(static record => record.TimeToLive) <= TimeSpan.Zero
                ? _options.DefaultCacheTtl
                : records.Min(static record => record.TimeToLive);
        _cache[domainName] = new CacheEntry(hosts, now.Add(ttl));
        return hosts;
    }

    private sealed record CacheEntry(
        IReadOnlyList<string> Hosts,
        DateTimeOffset ExpiresUtc);
}
