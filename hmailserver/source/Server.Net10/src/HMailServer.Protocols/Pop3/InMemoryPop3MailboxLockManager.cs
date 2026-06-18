using System.Collections.Concurrent;
using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Pop3;

public sealed class InMemoryPop3MailboxLockManager : IPop3MailboxLockManager
{
    private readonly ConcurrentDictionary<int, byte> _lockedAccounts = new();

    public ValueTask<IAsyncDisposable?> TryAcquireAsync(
        ImapAuthenticatedAccount account,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult<IAsyncDisposable?>(
            _lockedAccounts.TryAdd(account.AccountId, 0)
                ? new Lease(_lockedAccounts, account.AccountId)
                : null);
    }

    private sealed class Lease : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<int, byte> _lockedAccounts;
        private readonly int _accountId;
        private int _disposed;

        public Lease(
            ConcurrentDictionary<int, byte> lockedAccounts,
            int accountId)
        {
            _lockedAccounts = lockedAccounts;
            _accountId = accountId;
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _lockedAccounts.TryRemove(_accountId, out _);
            }

            return ValueTask.CompletedTask;
        }
    }
}
