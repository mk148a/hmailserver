using HMailServer.ComInterop;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class BackupRestoreTransactionBoundaryTests
{
    [TestMethod]
    public async Task ExecuteAsync_CommitsAfterMutation()
    {
        var events = new List<string>();

        await BackupRestoreTransactionBoundary.ExecuteAsync(
            _ =>
            {
                events.Add("mutate");
                return ValueTask.CompletedTask;
            },
            _ =>
            {
                events.Add("commit");
                return ValueTask.CompletedTask;
            },
            () =>
            {
                events.Add("rollback");
                return ValueTask.CompletedTask;
            },
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "mutate", "commit" }, events);
    }

    [TestMethod]
    public async Task ExecuteAsync_RollsBackAndRethrowsMutationFailure()
    {
        var events = new List<string>();
        var failure = new InvalidOperationException("mutation failed");

        var thrown = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await BackupRestoreTransactionBoundary.ExecuteAsync(
                _ =>
                {
                    events.Add("mutate");
                    return ValueTask.FromException(failure);
                },
                _ =>
                {
                    events.Add("commit");
                    return ValueTask.CompletedTask;
                },
                () =>
                {
                    events.Add("rollback");
                    return ValueTask.CompletedTask;
                },
                CancellationToken.None));

        Assert.AreSame(failure, thrown);
        CollectionAssert.AreEqual(new[] { "mutate", "rollback" }, events);
    }

    [TestMethod]
    public async Task ExecuteAsync_RollsBackWhenCommitFails()
    {
        var events = new List<string>();
        var failure = new InvalidOperationException("commit failed");

        var thrown = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await BackupRestoreTransactionBoundary.ExecuteAsync(
                _ =>
                {
                    events.Add("mutate");
                    return ValueTask.CompletedTask;
                },
                _ =>
                {
                    events.Add("commit");
                    return ValueTask.FromException(failure);
                },
                () =>
                {
                    events.Add("rollback");
                    return ValueTask.CompletedTask;
                },
                CancellationToken.None));

        Assert.AreSame(failure, thrown);
        CollectionAssert.AreEqual(new[] { "mutate", "commit", "rollback" }, events);
    }

    [TestMethod]
    public async Task ExecuteAsync_CancellationBeforeMutationDoesNotCommitOrRollback()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var events = new List<string>();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await BackupRestoreTransactionBoundary.ExecuteAsync(
                _ =>
                {
                    events.Add("mutate");
                    return ValueTask.CompletedTask;
                },
                _ =>
                {
                    events.Add("commit");
                    return ValueTask.CompletedTask;
                },
                () =>
                {
                    events.Add("rollback");
                    return ValueTask.CompletedTask;
                },
                cancellation.Token));

        CollectionAssert.AreEqual(new[] { "rollback" }, events);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReportsRollbackFailureWithOriginalFailure()
    {
        var mutationFailure = new InvalidOperationException("mutation failed");
        var rollbackFailure = new IOException("rollback failed");

        var thrown = await Assert.ThrowsExactlyAsync<AggregateException>(async () =>
            await BackupRestoreTransactionBoundary.ExecuteAsync(
                _ => ValueTask.FromException(mutationFailure),
                _ => ValueTask.CompletedTask,
                () => ValueTask.FromException(rollbackFailure),
                CancellationToken.None));

        CollectionAssert.AreEquivalent(
            new Exception[] { mutationFailure, rollbackFailure },
            thrown.InnerExceptions.ToArray());
    }
}
