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
}