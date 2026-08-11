using HMailServer.Core.Abstractions;
using HMailServer.Security;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;

namespace HMailServer.Delivery;

public sealed class RemoteSmtpEndpointResolver : IRemoteSmtpEndpointResolver
{
    private const int MaxCnameRecursionDepth = 10;
    private readonly IDnsMxResolver _mxResolver;
    private readonly IDnsCnameResolver? _cnameResolver;
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
        _cnameResolver = mxResolver as IDnsCnameResolver;
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

            return await ResolveConfiguredRouteAsync(target, route, cancellationToken)
                .ConfigureAwait(false);
        }

        if (target.Kind == DeliveryTargetKind.RemoteDomain)
        {
            var mxHosts = await ResolveRemoteHostsAsync(target.DomainName, cancellationToken).ConfigureAwait(false);
            var connectionSecurity = target.RemoteConnectionSecurity switch
            {
                (int)RemoteSmtpConnectionSecurity.None => RemoteSmtpConnectionSecurity.None,
                (int)RemoteSmtpConnectionSecurity.Ssl => RemoteSmtpConnectionSecurity.Ssl,
                (int)RemoteSmtpConnectionSecurity.StartTlsOptional => RemoteSmtpConnectionSecurity.StartTlsOptional,
                (int)RemoteSmtpConnectionSecurity.StartTlsRequired => RemoteSmtpConnectionSecurity.StartTlsRequired,
                _ => throw new InvalidOperationException(
                    $"Global SMTP connection security value {target.RemoteConnectionSecurity} is invalid.")
            };
            var candidates = await ResolveRemoteAddressCandidatesAsync(
                mxHosts,
                connectionSecurity,
                target.VerifyRemoteSslCertificate,
                cancellationToken).ConfigureAwait(false);
            if (target.MaxNumberOfMxHosts > 0)
            {
                candidates = candidates.Take(target.MaxNumberOfMxHosts).ToArray();
            }

            if (candidates.Count == 0)
            {
                throw new IOException("No usable address was found for remote SMTP delivery.");
            }

            return candidates[0] with { Candidates = candidates };
        }

        throw new InvalidOperationException("Local delivery targets do not have remote SMTP endpoints.");
    }

    private async ValueTask<RemoteSmtpEndpoint> ResolveConfiguredRouteAsync(
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
                AddAddressCandidate(host, configuredAddress);
                continue;
            }

            IReadOnlyList<IPAddress> addresses;
            try
            {
                addresses = await _addressResolver
                    .ResolveAddressesAsync(host, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastResolutionFailure = ex;
                continue;
            }

            foreach (var address in addresses)
            {
                if (address is not null)
                {
                    AddAddressCandidate(host, address);
                }
            }
        }

        if (candidates.Count == 0)
        {
            throw new IOException("SMTP route host resolution failed.", lastResolutionFailure);
        }

        if (target.MaxNumberOfMxHosts > 0)
        {
            candidates = candidates.Take(target.MaxNumberOfMxHosts).ToList();
        }

        var first = candidates[0];
        return first with { Candidates = candidates.ToArray() };

        void AddAddressCandidate(string host, IPAddress address)
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
                ConnectionAddress: address.ToString(),
                EnforceLocalEndpointGuard: true));
        }
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

        if (lastResolutionFailure is not null && candidates.Count == 0)
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
                ConnectionAddress: connectionAddress,
                EnforceLocalEndpointGuard: true));
        }
    }

    private async ValueTask<IReadOnlyList<RemoteSmtpEndpoint>> ResolveRemoteAddressCandidatesAsync(
        IReadOnlyList<string> hosts,
        RemoteSmtpConnectionSecurity connectionSecurity,
        bool verifyRemoteSslCertificate,
        CancellationToken cancellationToken)
    {
        var candidates = new List<RemoteSmtpEndpoint>();
        var seenAddresses = new HashSet<IPAddress>();

        foreach (var host in hosts)
        {
            if (IPAddress.TryParse(host, out var literalAddress))
            {
                AddCandidate(host, literalAddress);
                continue;
            }

            IReadOnlyList<IPAddress> addresses;
            try
            {
                addresses = await _addressResolver
                    .ResolveAddressesAsync(host, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new IOException($"SMTP host address resolution failed for '{host}'.", ex);
            }

            foreach (var address in addresses)
            {
                if (address is not null)
                {
                    AddCandidate(host, address);
                }
            }
        }

        return candidates;

        void AddCandidate(string host, IPAddress address)
        {
            if (!seenAddresses.Add(address))
            {
                return;
            }

            candidates.Add(new RemoteSmtpEndpoint(
                host,
                Port: 25,
                connectionSecurity,
                VerifyRemoteSslCertificate: verifyRemoteSslCertificate,
                ConnectionAddress: address.ToString(),
                EnforceLocalEndpointGuard: true));
        }
    }

    private async ValueTask<IReadOnlyList<string>> ResolveRemoteHostsAsync(
        string domainName,
        CancellationToken cancellationToken)
    {
        return await ResolveRemoteHostsAsync(
            domainName,
            depth: 0,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<IReadOnlyList<string>> ResolveRemoteHostsAsync(
        string domainName,
        int depth,
        HashSet<string> visitedDomains,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domainName);

        if (!visitedDomains.Add(domainName.TrimEnd('.')))
        {
            throw new IOException("DNS CNAME recursion cycle detected.");
        }

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
            if (_cnameResolver is not null)
            {
                IReadOnlyList<DnsCnameRecord> cnameRecords;
                try
                {
                    cnameRecords = await _cnameResolver
                        .ResolveCnameAsync(domainName, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (IOException)
                {
                    cnameRecords = Array.Empty<DnsCnameRecord>();
                }
                if (cnameRecords.Count == 1)
                {
                    if (depth >= MaxCnameRecursionDepth)
                    {
                        throw new IOException("DNS CNAME recursion limit exceeded.");
                    }

                    var cnameTarget = cnameRecords[0].Target.TrimEnd('.');
                    if (string.IsNullOrWhiteSpace(cnameTarget) || cnameTarget == ".")
                    {
                        throw new IOException("DNS CNAME lookup returned an invalid target.");
                    }

                    hosts = (await ResolveRemoteHostsAsync(
                        cnameTarget,
                        depth + 1,
                        visitedDomains,
                        cancellationToken).ConfigureAwait(false)).ToArray();
                    _cache[domainName] = new CacheEntry(
                        hosts,
                        now.Add(GetCacheTtl(cnameRecords[0].TimeToLive)));
                    return hosts;
                }
            }

            hosts = [domainName];
        }

        var ttl = records.Count == 0
            ? _options.NegativeCacheTtl
            : GetCacheTtl(records.Min(static record => record.TimeToLive));
        _cache[domainName] = new CacheEntry(hosts, now.Add(ttl));
        return hosts;
    }

    private TimeSpan GetCacheTtl(TimeSpan ttl) =>
        ttl <= TimeSpan.Zero ? _options.DefaultCacheTtl : ttl;

    private sealed record CacheEntry(
        IReadOnlyList<string> Hosts,
        DateTimeOffset ExpiresUtc);
}
