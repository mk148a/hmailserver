using System.Collections.Concurrent;

namespace HMailServer.Core.Abstractions;

public interface IImapFolderChangeTracker
{
    long GetGeneration(int accountId);

    long PublishUpsert(ImapFolderAdministrationSnapshot folder);

    long PublishDeletion(int accountId, IReadOnlyCollection<int> deletedFolderIds);

    bool TryGetLatestChange(
        int accountId,
        int folderId,
        out ImapFolderChange change);
}

public sealed record ImapFolderChange(
    long Generation,
    ImapFolderAdministrationSnapshot? Folder,
    bool IsDeleted);

public sealed class ImapFolderChangeTracker : IImapFolderChangeTracker
{
    public static ImapFolderChangeTracker Shared { get; } = new();

    private readonly ConcurrentDictionary<int, AccountChanges> _accounts = new();

    public long GetGeneration(int accountId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(accountId);
        return _accounts.TryGetValue(accountId, out var changes)
            ? Volatile.Read(ref changes.Generation)
            : 0;
    }

    public long PublishUpsert(ImapFolderAdministrationSnapshot folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentOutOfRangeException.ThrowIfNegative(folder.AccountId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(folder.Id);

        var changes = _accounts.GetOrAdd(folder.AccountId, static _ => new AccountChanges());
        lock (changes.Sync)
        {
            var generation = checked(changes.Generation + 1);
            changes.Generation = generation;
            changes.LatestByFolderId[folder.Id] = new ImapFolderChange(
                generation,
                folder,
                IsDeleted: false);
            return generation;
        }
    }

    public long PublishDeletion(int accountId, IReadOnlyCollection<int> deletedFolderIds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(accountId);
        ArgumentNullException.ThrowIfNull(deletedFolderIds);
        foreach (var folderId in deletedFolderIds)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(folderId);
        }

        var changes = _accounts.GetOrAdd(accountId, static _ => new AccountChanges());
        lock (changes.Sync)
        {
            var generation = checked(changes.Generation + 1);
            changes.Generation = generation;
            foreach (var folderId in deletedFolderIds)
            {
                changes.LatestByFolderId[folderId] = new ImapFolderChange(
                    generation,
                    Folder: null,
                    IsDeleted: true);
            }

            return generation;
        }
    }

    public bool TryGetLatestChange(
        int accountId,
        int folderId,
        out ImapFolderChange change)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(accountId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(folderId);

        if (_accounts.TryGetValue(accountId, out var changes))
        {
            lock (changes.Sync)
            {
                if (changes.LatestByFolderId.TryGetValue(folderId, out change!))
                {
                    return true;
                }
            }
        }

        change = null!;
        return false;
    }

    private sealed class AccountChanges
    {
        public object Sync { get; } = new();

        public long Generation;

        public Dictionary<int, ImapFolderChange> LatestByFolderId { get; } = [];
    }
}
