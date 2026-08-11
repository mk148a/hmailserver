using HMailServer.Core.Abstractions;
using System.Collections.Concurrent;

namespace HMailServer.Delivery;

public sealed class RemoteSmtpEndpointResolver : IRemoteSmtpEndpointResolver
{
    private readonly IDnsMxResolver _mxResolver;
    private readonly RemoteSmtpEndpointResolverOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    public RemoteSmtpEndpointResolver()
        : this(new SystemDnsMxResolver(), RemoteSmtpEndpointResolverOptions.Default, TimeProvider.System)
    {
    }

    public RemoteSmtpEndpointResolver(
        IDnsMxResolver mxResolver,
        RemoteSmtpEndpointResolverOptions options)
        : this(mxResolver, options, TimeProvider.System)
    {
    }

    public RemoteSmtpEndpointResolver(
        IDnsMxResolver mxResolver,
        RemoteSmtpEndpointResolverOptions options,
        TimeProvider timeProvider)
    {
        _mxResolver = mxResolver;
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
            return new RemoteSmtpEndpoint(
                route.TargetHost,
                route.TargetPort <= 0 ? 25 : route.TargetPort,
                (RemoteSmtpConnectionSecurity)route.ConnectionSecurity,
                route.RequiresAuthentication,
                route.AuthenticationUsername,
                route.AuthenticationPassword);
        }

        if (target.Kind == DeliveryTargetKind.RemoteDomain)
        {
            var mxHost = await ResolveRemoteHostAsync(target.DomainName, cancellationToken).ConfigureAwait(false);
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
                connectionSecurity);
        }

        throw new InvalidOperationException("Local delivery targets do not have remote SMTP endpoints.");
    }

    private async ValueTask<string> ResolveRemoteHostAsync(
        string domainName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domainName);

        var now = _timeProvider.GetUtcNow();
        if (_cache.TryGetValue(domainName, out var cached) && cached.ExpiresUtc > now)
        {
            return cached.Host;
        }

        var records = await _mxResolver.ResolveMxAsync(domainName, cancellationToken).ConfigureAwait(false);
        var selected = records
            .OrderBy(static record => record.Preference)
            .ThenBy(static record => record.Exchange, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        var host = selected?.Exchange.TrimEnd('.') ?? domainName;
        var ttl = selected is null
            ? _options.NegativeCacheTtl
            : selected.TimeToLive <= TimeSpan.Zero
                ? _options.DefaultCacheTtl
                : selected.TimeToLive;
        _cache[domainName] = new CacheEntry(host, now.Add(ttl));
        return host;
    }

    private sealed record CacheEntry(
        string Host,
        DateTimeOffset ExpiresUtc);
}
