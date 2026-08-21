using HMailServer.Core.Abstractions;
using HMailServer.Service;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class DatabaseVersionStartupGuardTests
{
    [TestMethod]
    [DataRow(5708)]
    [DataRow(5999)]
    public async Task EnsureCompatibleAsync_RejectsLegacyOrPreRuntimeVersion(int currentVersion)
    {
        var guard = CreateGuard(currentVersion, isConnected: true);

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => guard.EnsureCompatibleAsync(CancellationToken.None).AsTask());

        StringAssert.Contains(exception.Message, $"{currentVersion}");
        StringAssert.Contains(exception.Message, "6000");
    }

    [TestMethod]
    public async Task EnsureCompatibleAsync_AllowsMigratedRuntimeVersion()
    {
        var guard = CreateGuard(6000, isConnected: true);

        await guard.EnsureCompatibleAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task EnsureCompatibleAsync_RejectsDisconnectedDatabaseBeforeVersionUse()
    {
        var guard = CreateGuard(null, isConnected: false);

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => guard.EnsureCompatibleAsync(CancellationToken.None).AsTask());

        StringAssert.Contains(exception.Message, "connection");
    }

    [TestMethod]
    public async Task EnsureCompatibleAsync_RejectsUnreadableVersion()
    {
        var guard = CreateGuard(null, isConnected: true);

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => guard.EnsureCompatibleAsync(CancellationToken.None).AsTask());

        StringAssert.Contains(exception.Message, "version");
    }

    private static DatabaseVersionStartupGuard CreateGuard(int? currentVersion, bool isConnected) =>
        new(new FixedDatabaseAdministrationStore(
            new DatabaseAdministrationSnapshot(
                RequiredVersion: 5708,
                CurrentVersion: currentVersion,
                DatabaseType: 2,
                DatabaseExists: true,
                IsConnected: isConnected,
                ServerName: "local",
                DatabaseName: "isolated")));

    private sealed class FixedDatabaseAdministrationStore(DatabaseAdministrationSnapshot snapshot)
        : IDatabaseAdministrationStore
    {
        public ValueTask<DatabaseAdministrationSnapshot> GetDatabaseAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(snapshot);
    }
}
