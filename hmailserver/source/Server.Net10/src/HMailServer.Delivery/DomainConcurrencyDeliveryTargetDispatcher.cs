using System.Collections.Concurrent;
using HMailServer.Core.Abstractions;

namespace HMailServer.Delivery;

public sealed class DomainConcurrencyDeliveryTargetDispatcher : IDeliveryTargetDispatcher
{
    private readonly IDeliveryTargetDispatcher _inner;
    private readonly DomainConcurrencyOptions _options;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _domainLocks = new(StringComparer.OrdinalIgnoreCase);

    public DomainConcurrencyDeliveryTargetDispatcher(
        IDeliveryTargetDispatcher inner,
        DomainConcurrencyOptions options)
    {
        _inner = inner;
        _options = options;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxConcurrentDeliveriesPerDomain);
    }

    public async ValueTask<DeliveryTargetDispatchResult> DispatchAsync(
        DeliveryQueuedMessage message,
        DeliveryTargetBatch targetBatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targetBatch);

        var key = GetConcurrencyKey(targetBatch.Target);
        var gate = _domainLocks.GetOrAdd(
            key,
            _ => new SemaphoreSlim(_options.MaxConcurrentDeliveriesPerDomain, _options.MaxConcurrentDeliveriesPerDomain));

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await _inner.DispatchAsync(message, targetBatch, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private static string GetConcurrencyKey(DeliveryTarget target)
    {
        if (target.Kind == DeliveryTargetKind.Route && target.Route is not null)
        {
            return "route:" + target.Route.RouteId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return string.IsNullOrWhiteSpace(target.DomainName)
            ? target.Key
            : target.DomainName;
    }
}
