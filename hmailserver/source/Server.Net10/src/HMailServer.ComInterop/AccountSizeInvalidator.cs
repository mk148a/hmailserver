using System.Collections.Concurrent;

namespace HMailServer.ComInterop;

internal sealed class AccountSizeInvalidator
{
    private readonly ConcurrentDictionary<int, long> _versions = new();

    internal void Register(int accountId)
    {
        if (accountId > 0)
        {
            _versions.TryAdd(accountId, 0);
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

    internal void Reset() => _versions.Clear();
}
