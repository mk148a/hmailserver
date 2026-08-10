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
