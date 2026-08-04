using System.Collections.Concurrent;

namespace HMailServer.ComInterop;

internal sealed class AccountSizeInvalidator
{
    private readonly ConcurrentDictionary<int, long> _versions = new();
    private readonly ConcurrentDictionary<object, ConcurrentDictionary<int, byte>> _registrations = new();
    private readonly object _directRegistrationOwner = new();

    internal void Register(int accountId)
        => Register(_directRegistrationOwner, accountId);

    internal void Register(object owner, int accountId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (accountId <= 0)
        {
            return;
        }

        var registrations = _registrations.GetOrAdd(
            owner,
            static _ => new ConcurrentDictionary<int, byte>());
        if (registrations.TryAdd(accountId, 0))
        {
            _versions.TryAdd(accountId, 0);
        }
    }

    internal void Reconcile(object owner, IReadOnlyCollection<int> accountIds)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(accountIds);

        var currentIds = accountIds
            .Where(accountId => accountId > 0)
            .ToHashSet();
        var registrations = _registrations.GetOrAdd(
            owner,
            static _ => new ConcurrentDictionary<int, byte>());

        foreach (var registeredId in registrations.Keys)
        {
            if (!currentIds.Contains(registeredId))
            {
                registrations.TryRemove(registeredId, out _);
                if (!_registrations.Values.Any(registration => registration.ContainsKey(registeredId)))
                {
                    _versions.TryRemove(registeredId, out _);
                }
            }
        }

        foreach (var accountId in currentIds)
        {
            if (registrations.TryAdd(accountId, 0))
            {
                _versions.TryAdd(accountId, NextGeneration());
            }
        }
    }

    internal long GetVersion(int accountId) =>
        accountId > 0 && _versions.TryGetValue(accountId, out var version)
            ? version
            : 0;

    internal void Invalidate(int accountId)
    {
        if (accountId <= 0)
        {
            return;
        }

        try
        {
            while (_versions.TryGetValue(accountId, out var version))
            {
                var nextVersion = version == long.MaxValue ? 1 : version + 1;
                if (_versions.TryUpdate(accountId, nextVersion, version))
                {
                    return;
                }
            }
        }
        catch (Exception)
        {
            // Invalidation is best effort and must never affect the committed mutation.
        }
    }

    internal void Reset()
    {
        _registrations.Clear();
        _versions.Clear();
    }

    private long NextGeneration()
    {
        var generation = Interlocked.Increment(ref _nextGeneration);
        return generation == 0 ? Interlocked.Increment(ref _nextGeneration) : generation;
    }

    private long _nextGeneration;
}
