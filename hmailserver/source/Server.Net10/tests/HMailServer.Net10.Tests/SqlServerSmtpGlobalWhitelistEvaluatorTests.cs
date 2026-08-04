using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerSmtpGlobalWhitelistEvaluatorTests
{
    [TestMethod]
    public async Task EvaluateAsync_LoadsCurrentWhitelistForEachRequestAndMatchesSenderAndClient()
    {
        var store = new FakeWhiteListAddressAdministrationStore
        {
            Addresses =
            [
                new WhiteListAddressAdministrationSnapshot(
                    1,
                    "192.0.2.10",
                    "192.0.2.20",
                    "sender@example.test",
                    "test")
            ]
        };
        var evaluator = new SqlServerSmtpGlobalWhitelistEvaluator(store);

        Assert.IsTrue(await evaluator.EvaluateAsync("sender@example.test", "192.0.2.10", CancellationToken.None));
        Assert.IsTrue(await evaluator.EvaluateAsync("sender@example.test", "192.0.2.20", CancellationToken.None));
        Assert.AreEqual(2, store.LoadCount);
    }

    [TestMethod]
    public async Task EvaluateAsync_ReturnsFalseWhenWhitelistLoadFails()
    {
        var store = new FakeWhiteListAddressAdministrationStore
        {
            Load = _ => throw new InvalidOperationException("database unavailable")
        };
        var evaluator = new SqlServerSmtpGlobalWhitelistEvaluator(store);

        var result = await evaluator.EvaluateAsync("sender@example.test", "192.0.2.10", CancellationToken.None);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task EvaluateAsync_PropagatesCancellation()
    {
        var store = new FakeWhiteListAddressAdministrationStore
        {
            Load = cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult<IReadOnlyList<WhiteListAddressAdministrationSnapshot>>([]);
            }
        };
        var evaluator = new SqlServerSmtpGlobalWhitelistEvaluator(store);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            await evaluator.EvaluateAsync("sender@example.test", "192.0.2.10", cancellation.Token);
            Assert.Fail("Cancellation should propagate.");
        }
        catch (OperationCanceledException)
        {
        }
    }

    private sealed class FakeWhiteListAddressAdministrationStore : IWhiteListAddressAdministrationStore
    {
        public IReadOnlyList<WhiteListAddressAdministrationSnapshot> Addresses { get; init; } = [];

        public Func<CancellationToken, ValueTask<IReadOnlyList<WhiteListAddressAdministrationSnapshot>>>? Load { get; init; }

        public int LoadCount { get; private set; }

        public ValueTask<IReadOnlyList<WhiteListAddressAdministrationSnapshot>> GetWhiteListAddressesAsync(
            CancellationToken cancellationToken)
        {
            LoadCount++;
            if (Load is not null)
            {
                return Load(cancellationToken);
            }

            return ValueTask.FromResult(Addresses);
        }

        public ValueTask<long> InsertWhiteListAddressAsync(
            WhiteListAddressAdministrationSnapshot address,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(0L);

        public ValueTask UpdateWhiteListAddressAsync(
            WhiteListAddressAdministrationSnapshot address,
            CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask<bool> DeleteWhiteListAddressByIdAsync(
            long databaseId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(false);

        public ValueTask ClearWhiteListAddressesAsync(CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }
}
