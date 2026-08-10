using System.Collections.Concurrent;
using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Pop3;

public sealed class InMemoryPop3MailboxLockManager : IPop3MailboxLockManager
{
    private readonly ConcurrentDictionary<int, object> _lockedAccounts = new();

    public void Unlock(int accountId) => _lockedAccounts.TryRemove(accountId, out _);

    public ValueTask<IAsyncDisposable?> TryAcquireAsync(
        ImapAuthenticatedAccount account,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        cancellationToken.ThrowIfCancellationRequested();

        var leaseOwner = new object();
        return ValueTask.FromResult<IAsyncDisposable?>(
            _lockedAccounts.TryAdd(account.AccountId, leaseOwner)
                ? new Lease(_lockedAccounts, account.AccountId, leaseOwner)
                : null);
    }

    private sealed class Lease : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<int, object> _lockedAccounts;
        private readonly int _accountId;
        private readonly object _leaseOwner;
        private int _disposed;

        public Lease(
            ConcurrentDictionary<int, object> lockedAccounts,
            int accountId,
            object leaseOwner)
        {
            _lockedAccounts = lockedAccounts;
            _accountId = accountId;
            _leaseOwner = leaseOwner;
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                ((ICollection<KeyValuePair<int, object>>)_lockedAccounts)
                    .Remove(new KeyValuePair<int, object>(_accountId, _leaseOwner));
            }

            return ValueTask.CompletedTask;
        }
    }
}
