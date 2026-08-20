using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class BackupRestoreMetadataWriterTests
{
    private static readonly DomainAdministrationSnapshot Alpha = new(0, "alpha.example", true);
    private static readonly DomainAdministrationSnapshot Beta = new(0, "beta.example", false);

    [TestMethod]
    public async Task RestoreDomainsAsync_PersistsAllAndInvokesCommitRollback()
    {
        var store = new RecordingDomainStore();
        var rollbackCalls = 0;

        var result = await BackupRestoreMetadataWriter.RestoreDomainsAsync(
            new[] { Alpha, Beta },
            store,
            () =>
            {
                rollbackCalls++;
                return default;
            },
            CancellationToken.None);

        Assert.AreEqual(2, result.RestoredDomains);
        Assert.AreEqual(2, store.Inserted.Count);
        Assert.AreEqual("alpha.example", store.Inserted[0].Name);
        Assert.AreEqual("beta.example", store.Inserted[1].Name);
        Assert.AreEqual(0, rollbackCalls);
    }

    [TestMethod]
    public async Task RestoreDomainsAsync_RollsBackWhenInsertFailsAndRethrows()
    {
        var store = new FailingDomainStore(failOnName: "beta.example");
        var rollbackCalls = 0;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => BackupRestoreMetadataWriter.RestoreDomainsAsync(
                new[] { Alpha, Beta },
                store,
                () =>
                {
                    rollbackCalls++;
                    return default;
                },
                CancellationToken.None).AsTask());

        Assert.AreEqual(1, store.Inserted.Count);
        Assert.AreEqual("alpha.example", store.Inserted[0].Name);
        Assert.AreEqual(1, rollbackCalls);
    }

    [TestMethod]
    public async Task RestoreFoldersAsync_TracksRootBeforeMessageInsertFailure()
    {
        var folderStore = new RecordingFolderRestoreStore();
        var rootIds = new List<int>();
        var rollbackCalls = 0;
        var folder = new RestoreFolderEntry(
            new ImapFolderAdministrationSnapshot(0, 0, -1, "INBOX", true, 5, "2026-07-01 12:30:00"),
            Array.Empty<RestoreFolderEntry>(),
            new[]
            {
                new MessageAdministrationSnapshot(
                    0,
                    0,
                    0,
                    "one.eml",
                    2,
                    "sender@example.test",
                    42,
                    0,
                    1,
                    new DateTime(2026, 7, 1, 12, 32, 0, DateTimeKind.Unspecified),
                    8)
            });

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => BackupRestoreMetadataWriter.RestoreFoldersAsync(
                new[] { folder },
                accountId: 7,
                folderStore,
                new FailingMessageRestoreStore(),
                () =>
                {
                    rollbackCalls++;
                    return default;
                },
                CancellationToken.None,
                rootIds.Add).AsTask());

        Assert.AreEqual(1, rootIds.Count);
        Assert.AreEqual(101, rootIds[0]);
        Assert.AreEqual(1, rollbackCalls);
    }

    [TestMethod]
    public async Task RestorePublicFolderPermissionsAsync_ResolvesAndInsertsInArchiveOrder()
    {
        var store = new RecordingFolderPermissionRestoreStore();
        var permissions = new[]
        {
            new RestoreFolderPermissionEntry(0, 3, "user@example.test"),
            new RestoreFolderPermissionEntry(1, 1024, "Editors"),
            new RestoreFolderPermissionEntry(2, 2047, "ignored")
        };

        var restored = await BackupRestoreMetadataWriter.RestorePublicFolderPermissionsAsync(
            permissions,
            folderId: 500,
            new[] { new AccountAdministrationSnapshot(42, 7, "user@example.test", true, 0) },
            new[] { new GroupAdministrationSnapshot(77, "Editors") },
            store,
            () => default,
            CancellationToken.None);

        Assert.AreEqual(3, restored);
        CollectionAssert.AreEqual(
            new[]
            {
                (FolderId: 500, Type: 0, GroupId: 0, AccountId: 42, Rights: 3),
                (FolderId: 500, Type: 1, GroupId: 77, AccountId: 0, Rights: 1024),
                (FolderId: 500, Type: 2, GroupId: 0, AccountId: 0, Rights: 2047)
            },
            store.Inserted);
    }

    [TestMethod]
    public async Task RestorePublicFolderPermissionsAsync_ResolvesBeforeAnyInsertOnInvalidHolder()
    {
        var store = new RecordingFolderPermissionRestoreStore();
        var rollbackCalls = 0;
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
            BackupRestoreMetadataWriter.RestorePublicFolderPermissionsAsync(
                new[]
                {
                    new RestoreFolderPermissionEntry(0, 3, "user@example.test"),
                    new RestoreFolderPermissionEntry(1, 1024, "MissingGroup")
                },
                folderId: 500,
                new[] { new AccountAdministrationSnapshot(42, 7, "user@example.test", true, 0) },
                new[] { new GroupAdministrationSnapshot(77, "Editors") },
                store,
                () =>
                {
                    rollbackCalls++;
                    return default;
                },
                CancellationToken.None).AsTask());

        Assert.IsEmpty(store.Inserted);
        Assert.AreEqual(0, rollbackCalls);
    }

    [TestMethod]
    public async Task RestorePublicFolderPermissionsAsync_RollsBackAfterMidBatchInsertFailure()
    {
        var store = new RecordingFolderPermissionRestoreStore(failOnInsert: 2);
        var rollbackCalls = 0;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            BackupRestoreMetadataWriter.RestorePublicFolderPermissionsAsync(
                new[]
                {
                    new RestoreFolderPermissionEntry(0, 3, "user@example.test"),
                    new RestoreFolderPermissionEntry(1, 1024, "Editors")
                },
                folderId: 500,
                new[] { new AccountAdministrationSnapshot(42, 7, "user@example.test", true, 0) },
                new[] { new GroupAdministrationSnapshot(77, "Editors") },
                store,
                () =>
                {
                    rollbackCalls++;
                    return default;
                },
                CancellationToken.None).AsTask());

        Assert.AreEqual(1, store.Inserted.Count);
        Assert.AreEqual(1, rollbackCalls);
    }

    [TestMethod]
    public async Task RestorePublicFoldersAsync_ProcessesMessagesChildrenThenAclInLegacyOrder()
    {
        var events = new List<string>();
        var folderStore = new RecordingPublicFolderRestoreStore(events);
        var messageStore = new RecordingPublicMessageRestoreStore(events);
        var permissionStore = new RecordingFolderPermissionRestoreStore(events: events);
        var root = new RestorePublicFolderEntry(
            new ImapFolderAdministrationSnapshot(0, 0, -1, "Shared", true, 4, "2026-07-01 12:30:00"),
            new[]
            {
                new RestorePublicFolderEntry(
                    new ImapFolderAdministrationSnapshot(0, 0, -1, "Child", true, 2, "2026-07-01 12:31:00"),
                    Array.Empty<RestorePublicFolderEntry>(),
                    Array.Empty<MessageAdministrationSnapshot>(),
                    new[] { new RestoreFolderPermissionEntry(1, 1024, "Editors") })
            },
            new[]
            {
                new MessageAdministrationSnapshot(
                    0, 0, 0, "root.eml", 2, "sender@example.test", 42, 0, 1,
                    new DateTime(2026, 7, 1, 12, 32, 0), 8)
            },
            new[] { new RestoreFolderPermissionEntry(0, 3, "user@example.test") });

        var result = await BackupRestoreMetadataWriter.RestorePublicFoldersAsync(
            new[] { root },
            new[] { new AccountAdministrationSnapshot(42, 7, "user@example.test", true, 0) },
            new[] { new GroupAdministrationSnapshot(77, "Editors") },
            folderStore,
            messageStore,
            permissionStore,
            () => default,
            CancellationToken.None);

        Assert.AreEqual(2, result.RestoredFolders);
        Assert.AreEqual(1, result.RestoredMessages);
        CollectionAssert.AreEqual(
            new[]
            {
                "folder:Shared:0:-1",
                "message:500:root.eml:0",
                "folder:Child:0:500",
                "acl:501:1:77:0:1024",
                "acl:500:0:0:42:3"
            },
            events);
    }

    private sealed class RecordingDomainStore : IDomainAdministrationStore
    {
        public List<DomainAdministrationSnapshot> Inserted { get; } = new();

        public ValueTask<IReadOnlyList<DomainAdministrationSnapshot>> GetDomainsAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<DomainAdministrationSnapshot>>(Array.Empty<DomainAdministrationSnapshot>());

        public ValueTask<int> InsertDomainAsync(DomainAdministrationSnapshot domain, CancellationToken cancellationToken)
        {
            Inserted.Add(domain);
            return ValueTask.FromResult(Inserted.Count);
        }
    }

    private sealed class FailingDomainStore : IDomainAdministrationStore
    {
        private readonly string _failOnName;

        public FailingDomainStore(string failOnName)
        {
            _failOnName = failOnName;
        }

        public List<DomainAdministrationSnapshot> Inserted { get; } = new();

        public ValueTask<IReadOnlyList<DomainAdministrationSnapshot>> GetDomainsAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<DomainAdministrationSnapshot>>(Array.Empty<DomainAdministrationSnapshot>());

        public ValueTask<int> InsertDomainAsync(DomainAdministrationSnapshot domain, CancellationToken cancellationToken)
        {
            if (domain.Name == _failOnName)
            {
                throw new InvalidOperationException("Simulated restore insert failure.");
            }

            Inserted.Add(domain);
            return ValueTask.FromResult(Inserted.Count);
        }
    }

    private sealed class RecordingFolderRestoreStore : IImapFolderAdministrationRestoreStore
    {
        public ValueTask<ImapFolderAdministrationSnapshot> InsertFolderForRestoreAsync(
            ImapFolderAdministrationSnapshot folder,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(folder with { Id = 101 });
    }

    private sealed class RecordingFolderPermissionRestoreStore
        : IImapFolderPermissionAdministrationRestoreStore
    {
        private readonly int? _failOnInsert;
        private readonly List<string>? _events;

        public RecordingFolderPermissionRestoreStore(
            int? failOnInsert = null,
            List<string>? events = null)
        {
            _failOnInsert = failOnInsert;
            _events = events;
        }

        public List<(int FolderId, int Type, int GroupId, int AccountId, int Rights)> Inserted { get; } = new();

        public ValueTask<ImapFolderPermissionAdministrationSnapshot?> InsertFolderPermissionForRestoreAsync(
            int folderId,
            int permissionType,
            int permissionGroupId,
            int permissionAccountId,
            int value,
            CancellationToken cancellationToken)
        {
            if (_failOnInsert == Inserted.Count + 1)
            {
                throw new InvalidOperationException("Simulated ACL insert failure.");
            }

            Inserted.Add((folderId, permissionType, permissionGroupId, permissionAccountId, value));
            _events?.Add($"acl:{folderId}:{permissionType}:{permissionGroupId}:{permissionAccountId}:{value}");
            return ValueTask.FromResult<ImapFolderPermissionAdministrationSnapshot?>(
                new ImapFolderPermissionAdministrationSnapshot(
                    Inserted.Count,
                    folderId,
                    permissionType,
                    permissionGroupId,
                    permissionAccountId,
                    value));
        }
    }

    private sealed class RecordingPublicFolderRestoreStore : IImapFolderAdministrationRestoreStore
    {
        private readonly List<string> _events;
        private int _nextId = 500;

        public RecordingPublicFolderRestoreStore(List<string> events)
        {
            _events = events;
        }

        public ValueTask<ImapFolderAdministrationSnapshot> InsertFolderForRestoreAsync(
            ImapFolderAdministrationSnapshot folder,
            CancellationToken cancellationToken)
        {
            var inserted = folder with { Id = _nextId++ };
            _events.Add($"folder:{inserted.Name}:{inserted.AccountId}:{inserted.ParentId}");
            return ValueTask.FromResult(inserted);
        }
    }

    private sealed class RecordingPublicMessageRestoreStore : IMessageAdministrationRestoreStore
    {
        private readonly List<string> _events;
        private long _nextId = 700;

        public RecordingPublicMessageRestoreStore(List<string> events)
        {
            _events = events;
        }

        public ValueTask<MessageAdministrationInsertResult> InsertMessageForRestoreAsync(
            int accountId,
            int folderId,
            MessageAdministrationSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            _events.Add($"message:{folderId}:{snapshot.FileName}:{accountId}");
            return ValueTask.FromResult(new MessageAdministrationInsertResult(_nextId++, snapshot.Uid, snapshot.State));
        }
    }

    private sealed class FailingMessageRestoreStore : IMessageAdministrationRestoreStore
    {
        public ValueTask<MessageAdministrationInsertResult> InsertMessageForRestoreAsync(
            int accountId,
            int folderId,
            MessageAdministrationSnapshot snapshot,
            CancellationToken cancellationToken) =>
            ValueTask.FromException<MessageAdministrationInsertResult>(
                new InvalidOperationException("Simulated message restore failure."));
    }
}
