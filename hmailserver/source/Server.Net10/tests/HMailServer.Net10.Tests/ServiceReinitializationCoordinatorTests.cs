using HMailServer.Service;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ServiceReinitializationCoordinatorTests
{
    [TestMethod]
    public async Task ReinitializeAsync_StopsInReverseOrderAndStartsInRegistrationOrder()
    {
        var calls = new List<string>();
        var coordinator = new ServiceReinitializationCoordinator();
        coordinator.Register("bootstrap", _ => RecordAsync(calls, "stop-bootstrap"), _ => RecordAsync(calls, "start-bootstrap"));
        coordinator.Register("listeners", _ => RecordAsync(calls, "stop-listeners"), _ => RecordAsync(calls, "start-listeners"));

        await coordinator.ReinitializeAsync(CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "stop-listeners", "stop-bootstrap", "start-bootstrap", "start-listeners" },
            calls);
    }

    [TestMethod]
    public void Register_RejectsDuplicateNames()
    {
        var coordinator = new ServiceReinitializationCoordinator();
        coordinator.Register("listeners", _ => ValueTask.CompletedTask, _ => ValueTask.CompletedTask);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            coordinator.Register("listeners", _ => ValueTask.CompletedTask, _ => ValueTask.CompletedTask));
    }

    [TestMethod]
    public async Task ReinitializeAsync_FailsClosedWhenNoParticipantsAreRegistered()
    {
        var coordinator = new ServiceReinitializationCoordinator();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => coordinator.ReinitializeAsync(CancellationToken.None).AsTask());
    }

    [TestMethod]
    public async Task ReinitializeAsync_RestartsEarlierStoppedParticipantsWhenLaterStopFails()
    {
        var expected = new InvalidOperationException("stop failed");
        var calls = new List<string>();
        var coordinator = new ServiceReinitializationCoordinator();
        coordinator.Register(
            "bootstrap",
            _ =>
            {
                calls.Add("stop-bootstrap");
                return ValueTask.FromException(expected);
            },
            _ => RecordAsync(calls, "start-bootstrap"));
        coordinator.Register(
            "listeners",
            _ => RecordAsync(calls, "stop-listeners"),
            _ => RecordAsync(calls, "start-listeners"));

        var actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => coordinator.ReinitializeAsync(CancellationToken.None).AsTask());

        Assert.AreSame(expected, actual);
        CollectionAssert.AreEqual(
            new[] { "stop-listeners", "stop-bootstrap", "start-listeners" },
            calls);
    }

    [TestMethod]
    public async Task ReinitializeAsync_StopsEarlierStartedParticipantsWhenLaterStartFails()
    {
        var expected = new InvalidOperationException("start failed");
        var calls = new List<string>();
        var coordinator = new ServiceReinitializationCoordinator();
        coordinator.Register(
            "bootstrap",
            _ => RecordAsync(calls, "stop-bootstrap"),
            _ => RecordAsync(calls, "start-bootstrap"));
        coordinator.Register(
            "listeners",
            _ => RecordAsync(calls, "stop-listeners"),
            _ => ValueTask.FromException(expected));

        var actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => coordinator.ReinitializeAsync(CancellationToken.None).AsTask());

        Assert.AreSame(expected, actual);
        CollectionAssert.AreEqual(
            new[] { "stop-listeners", "stop-bootstrap", "start-bootstrap", "stop-bootstrap" },
            calls);
    }

    [TestMethod]
    public void Register_RejectsAfterFirstReinitializeAttempt()
    {
        var coordinator = new ServiceReinitializationCoordinator();
        coordinator.Register("bootstrap", _ => ValueTask.CompletedTask, _ => ValueTask.CompletedTask);

        coordinator.ReinitializeAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            coordinator.Register("listeners", _ => ValueTask.CompletedTask, _ => ValueTask.CompletedTask));
    }

    private static ValueTask RecordAsync(List<string> calls, string value)
    {
        calls.Add(value);
        return ValueTask.CompletedTask;
    }
}
